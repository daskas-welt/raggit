# Quickstart: RAGGit Offline-Mode (Single-Tenant)

**Feature**: `001-offline-mode` | **Branch**: `001-offline-mode` | **Spec**: [spec.md](./spec.md)

## Prereqs

- .NET 8 SDK (`dotnet --version` ≥8.0), Git LFS for ONNX models (optional)
- AI Workstation: 16GB RAM + 10GB free disk, LAN to clients, WAN optional (must work WAN-off per SC-002)
- Client: Windows 11 (MAUI desktop) + iOS/Android (MAUI mobile, same codebase), 4GB RAM thin, LAN/VPN to workstation

## 1. Clone & Build

```powershell
git clone <repo> raggit; cd raggit
dotnet workload install maui   # one-time MAUI workload prerequisite
dotnet build RAGGit.sln
```

## 2. AI Workstation — One-Time Model Cache (LAN Only, No Cloud at Query Time)

**Option A: Ollama (recommended, one binary for embed+chat)**

```powershell
# Install Ollama (https://ollama.com) then:
ollama pull nomic-embed-text   # 768d, or all-minilm 384d for small disk
ollama pull llama3.2:3b        # quantized Q4, ~2GB (or phi3:mini)
ollama serve  # serves http://localhost:11434
```

**Option B: Pure .NET ONNX + LLamaSharp (no Ollama daemon)**

```powershell
git clone https://huggingface.co/TaylorAI/bge-micro-v2
git clone https://huggingface.co/microsoft/Phi-3-mini-4k-instruct-onnx
# Place under ./models/bge-micro-v2 and ./models/phi-3-mini as per SEMANTIC KERNEL OnnxSimpleRAG README
# GGUF alternative: download e.g. llama3.2-3b.Q4_K_M.gguf to ./models/
```

Configure `RAGGit.Workstation.Api/appsettings.json` (v1.1.0 — `VectorDb:VectorSize` 384|768 validated at startup per FR-001; legacy `Qdrant:Path` deprecated, warns if disagreeing):

```json
{
  "VectorDb": { "Path": "./data/lancedb", "Provider": "LanceDB", "VectorSize": 384 },
  "Qdrant": { "Path": "./data/qdrant" },
  "Ollama": { "Url": "http://localhost:11434", "EmbedModel": "all-minilm", "ChatModel": "phi3:mini", "TimeoutMs": 5000 },
  "Onnx": { "EmbeddingModelPath": "./models/bge-micro-v2/onnx/model.onnx", "ChatModelPath": "./models/phi-3-mini/cpu-int4" },
  "Cache": { "Enabled": true, "EmbedCap": 10000, "EmbedTTLHours": 24, "DocsMaxAgeSec": 30 }
}
```
Dev defaults `all-minilm`/`phi3:mini`/384 (prod `nomic-embed-text`/`llama3.2:3b`/768 commented in `appsettings.Development.json`). Corrupted `pdf`/`docx` returns `400 {error:"corrupted pdf"}` per FR-006 (002 hardening, no partial index). Tunable without redeploy for 200 users × 50+ q/day (`Trim/Lowercase/Punctuation` normalized `CachedEmbedder` LRU 10k ≈ 30MB, 24h TTL; `GET /api/documents` `Cache-Control: max-age=30` + `ETag`). Dev override `appsettings.Development.json` `EmbedCap 1000 / TTL 1h / max-age 10s`.

## 3. Run Workstation API

```powershell
dotnet run --project src/RAGGit.Workstation.Api --urls http://0.0.0.0:5001
# Swagger: http://ai-workstation.local:5001/swagger
# Health: GET http://ai-workstation.local:5001/health → 200 {vectorDb: ok, llm: ok, version:"1.1.0"} (qdrant key retained for backward compatibility)
# Auth: GET /api/auth/me -H "X-Api-Key: <key>" → 200 {identityType:"ApiKey", role:"Admin"|"Employee"} (1.1.0, Q2 extensible envelope)
```

## 4. Run Client (Thin, No Models — .NET MAUI, v1.1.0)

```powershell
# Windows 11 desktop (MAUI workload installed in step 1)
dotnet run --project src/RAGGit.Client.Maui -f net8.0-windows10.0.19041.0
# Fallback net8.0 CI (no workload, views excluded, logic testable):
dotnet run --project src/RAGGit.Client.Maui -f net8.0
# Client reads src/RAGGit.Client.Maui/appsettings.json (Workstation:Url, Workstation:ApiKey > Api:AdminKey/Api:EmployeeKey)
# and discovers role via GET /api/auth/me at launch (FR-003/FR-004); legacy --workstation flags are fictional

# Publish per target TFM (signed per Constitution VII: MSIX for Windows, private enterprise distribution for mobile)
dotnet publish src/RAGGit.Client.Maui -c Release -f net8.0-windows10.0.19041.0   # Windows 11 → MSIX
dotnet publish src/RAGGit.Client.Maui -c Release -f net8.0-android                # Android → sideload / private MDM
dotnet publish src/RAGGit.Client.Maui -c Release -f net8.0-ios                    # iOS → Ad-Hoc/enterprise (Mac build host required for signing)

# Mobile devices reach the workstation over site VPN or the same LAN subnet — no cloud relay (Constitution IV)
```

Client flows: **Admin**: `Library → Upload (PDF/docx/txt/md <100MB)` → status `Indexing→Ready`; **Employee**: `Query → "what is refund policy?"` → `{answer, citations[]}`.

## 5. Verify Offline Invariant (SC-002)

```powershell
# On workstation, disable WAN (keep LAN):
# Windows: netsh interface set interface "Ethernet" admin=disable (WAN adapter) or firewall block 0.0.0.0/0
# Then from desktop on same LAN:
curl http://ai-workstation.local:5001/api/documents  # 200
curl -X POST http://ai-workstation.local:5001/api/query -H "Content-Type: application/json" -d '{"query":"refund policy"}'
# → 200 {answer, citations} with no WAN — proves FR-004
```

## 6. Tests (v1.1.0 — opt-in real gate)

```powershell
# Fast CI (fakes only, no Ollama — must stay green per SC-003):
dotnet test
dotnet test --filter "RequiresOllama!=true"
# Opt-in real loop (needs ollama serve + all-minilm/phi3:mini + fresh data/lancedb):
dotnet test --filter "RequiresOllama"
# Performance gate SC-001 (<300s, fake <4s):
pwsh ./scripts/measureIngestPerformance.ps1
# Corrupted pdf (FR-006 SC-005):
dotnet test --filter "Corrupted"
# Full validation at once:
./scripts/validate-quickstart.ps1
dotnet csharpier check .
```

## Env Secrets (no hardcoding)

```powershell
dotnet user-secrets set "Api:AdminKey" "<admin-key>" --project src/RAGGit.Workstation.Api
dotnet user-secrets set "Api:EmployeeKey" "<employee-key>" --project src/RAGGit.Workstation.Api
dotnet user-secrets set "Onnx:EmbeddingModelPath" "./models/bge-micro-v2/onnx/model.onnx" --project src/RAGGit.Workstation.Api
```

See [SECURITY.md](../../../SECURITY.md) for the single-tenant, signing, and
data-exclusion policy.
