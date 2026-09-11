# Quickstart: Real Workstation Bring-Up

**Feature**: `002-real-bringup` | **Branch**: `002-real-bringup` | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

This proves the offline loop for real: Ollama + LanceDB file-backed, no fakes, WAN-off query, thin MAUI client via LAN. Dev is one laptop; prod is a separate workstation box (end users never run the LLM).

## Prereqs

- .NET 8 SDK (`dotnet --version` ≥8.0), Git
- Ollama installed (`ollama --version`), 10GB free disk, LAN (or localhost) to workstation, WAN optional (must work WAN-off per SC-002)
- Dev laptop 8-16GB RAM (prod workstation 16GB + GPU recommended)

## 1. Pull real models (once, then WAN can be off)

```powershell
# Dev laptop baseline: all-minilm 384 + phi3:mini (RAM-friendly)
ollama pull all-minilm
ollama pull phi3:mini

# Prod workstation baseline (separate box, verify at deploy):
# ollama pull nomic-embed-text  # 768d, heavier
# ollama pull llama3.2:3b        # or phi3:mini if RAM constrained

ollama serve  # serves http://localhost:11434 — keep running
```

`src/RAGGit.Workstation.Api/appsettings.json` (dev defaults already):

```json
{
  "Api": { "AdminKey": "", "EmployeeKey": "" },
  "VectorDb": { "Path": "./data/lancedb", "Provider": "LanceDB", "VectorSize": 384 },
  "Qdrant": { "Path": "./data/qdrant" },
  "Ollama": { "Url": "http://localhost:11434", "EmbedModel": "all-minilm", "ChatModel": "phi3:mini" }
}
```

Secrets (no hardcoding):

```powershell
dotnet user-secrets set "Api:AdminKey" "dev-admin-key" --project src/RAGGit.Workstation.Api
dotnet user-secrets set "Api:EmployeeKey" "dev-employee-key" --project src/RAGGit.Workstation.Api
```

Note: `--workstation` / `--api-key` flags in some docs are fictional — the real client reads `appsettings.json` (`Workstation:Url`, keys) per FR-003 (see step 6). A `--workstation` flag does not exist in the current `MauiProgram.cs`.

## 2. Build

```powershell
git clone <repo> raggit; cd raggit
dotnet workload install maui  # one-time for MAUI TFMs (optional for net8.0 fallback)
dotnet build RAGGit.sln
```

## 3. Run workstation API (real LanceDB + real Ollama)

```powershell
# Fresh DB for first run (or after dimension swap):
Remove-Item -Recurse -Force ./data/lancedb -ErrorAction SilentlyContinue

dotnet run --project src/RAGGit.Workstation.Api --urls http://localhost:5001
# Health:
curl http://localhost:5001/health
# → 200 { "vectorDb":"ok", "llm":"ok", "qdrant":"ok", "version":"1.1.0" }
```

If you see startup failure `Configured VectorSize 768 does not match existing collection dimension 384 — delete data/lancedb or re-index`, see Guard demo (step 5).

## 4. Upload a real PDF and query (LAN or localhost, WAN simulated-off)

```powershell
# Admin upload (real PDF, <100MB, 512/50 chunking):
curl -X POST http://localhost:5001/api/documents -H "X-Api-Key: dev-admin-key" -F "file=@sample.pdf"
# → 201 { id, filename, hash, status:"Indexing" }

# Wait for indexing, then list:
curl http://localhost:5001/api/documents -H "X-Api-Key: dev-admin-key"
# → [{ status:"Ready" }]  # must appear in <5 min on dev laptop — record in verification.md (SC-001)

# Query with citations (WAN can be off — keep localhost, disable WiFi or block WAN):
curl -X POST http://localhost:5001/api/query -H "X-Api-Key: dev-employee-key" -H "Content-Type: application/json" -d '{"query":"refund policy"}'
# → 200 { "answer":"...", "citations":[{ "documentId":"...", "chunkId":"...", "text":"..." }], "latencyMs":1234 }
# or  → 200 { "answer":"no relevant content found", "citations":[] }

# Offline invariant check — no Ollama:
# Stop ollama (Ctrl+C on ollama serve), then query again:
curl -X POST http://localhost:5001/api/query -H "X-Api-Key: dev-employee-key" -H "Content-Type: application/json" -d '{"query":"refund policy"}'
# → 503 { "error":"model unavailable offline" }  # must not hang (FR-007)
# Restart ollama serve before next step

# Auth envelope (new in 1.1.0):
curl http://localhost:5001/api/auth/me -H "X-Api-Key: dev-admin-key"
# → 200 { "identityType":"ApiKey", "role":"Admin" }
curl http://localhost:5001/api/auth/me -H "X-Api-Key: dev-employee-key"
# → 200 { "identityType":"ApiKey", "role":"Employee" }

# Corrupted pdf (FR-006):
# Create a truncated pdf: head -c 100 sample.pdf > bad.pdf  (or use a binary stub)
curl -X POST http://localhost:5001/api/documents -H "X-Api-Key: dev-admin-key" -F "file=@bad.pdf"
# → 400 { "error":"corrupted pdf" }  # no partial index; GET /api/documents does not list it as Ready
```

## 5. Guard demo — switch dimension 384 ↔ 768 (FR-001/Q3)

```powershell
# Ingest one doc with 384 (as above), then swap config without wiping:
# Edit src/RAGGit.Workstation.Api/appsettings.json:
#   "VectorDb": { "VectorSize": 768 }, "Ollama": { "EmbedModel": "nomic-embed-text" }

dotnet run --project src/RAGGit.Workstation.Api --urls http://localhost:5001
# → startup fails fast:
#   Configured VectorSize 768 does not match existing collection dimension 384 — delete data/lancedb or re-index

# Recover:
Remove-Item -Recurse -Force ./data/lancedb
dotnet run --project src/RAGGit.Workstation.Api --urls http://localhost:5001
# Re-ingest with 768 — subsequent queries succeed
```

## 6. Thin MAUI client (Windows desktop or localhost dev)

Mobile on-device (iOS/Android) deferred per Q1 — Windows/localhost run is acceptance.

`src/RAGGit.Client.Maui/appsettings.json` sample:

```json
{
  "Workstation": {
    "Url": "http://localhost:5001",
    "ApiKey": "dev-employee-key"
  },
  "Api": {
    "AdminKey": "dev-admin-key",
    "EmployeeKey": "dev-employee-key"
  }
}
```

Run:

```powershell
# Windows 11 desktop (MAUI workload installed):
dotnet run --project src/RAGGit.Client.Maui -f net8.0-windows10.0.19041.0

# Or fallback net8.0 (views excluded, logic testable — no MAUI workload needed):
dotnet run --project src/RAGGit.Client.Maui -f net8.0
```

Behavior: client calls `GET /api/auth/me` at launch, learns `role`, renders `Upload`/`Delete` only for `Admin`; `Employee` sees read-only Library + Query. Kill the API → client shows `AI workstation unavailable` / `cannot reach AI workstation` within timeout, never cloud fallback (FR-007).

## 7. Opt-in real tests (skip gracefully when Ollama absent)

```powershell
# Default CI — fakes only (no Ollama needed, must stay green):
dotnet test

# Opt-in real loop (requires ollama serve with all-minilm + phi3:mini and a fresh data/lancedb):
dotnet test --filter "Trait=RequiresOllama"

# Or by trait exclusion:
dotnet test --filter "RequiresOllama!=true"  # fakes only explicitly
```

Real suite proves zero fakes on ingest → vector → query core path; when `http://localhost:11434` is absent it is skipped (SC-003).

## 8. Record SC-001/SC-002

After steps 4-5, fill `verification.md` with wall-clock numbers (see template). Dev laptop specs + "<7s on reference workstation is production assumption" note required.

## Troubleshooting

- `ollama pull` needs WAN — do it before WAN-off test; never at query time (Constitution IV).
- Legacy `./data/qdrant` — ignored; `VectorDb:Path` is truth. Startup log Warns if it exists and disagrees.
- 8GB laptop → keep `phi3:mini`; `llama3.2:3b` may OOM.
- ONNX/LLamaSharp — installed as packages but not acceptance for this feature (FR-005); leave `Ingest:Embedder` on default (Ollama).

