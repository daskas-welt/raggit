# Quickstart: RAGGit Offline-Mode (Single-Tenant)

**Feature**: `001-offline-mode` | **Branch**: `001-offline-mode` | **Spec**: [spec.md](./spec.md)

## Prereqs

- .NET 8 SDK (`dotnet --version` ≥8.0), Git LFS for ONNX models (optional)
- AI Workstation: 16GB RAM + 10GB free disk, LAN to clients, WAN optional (must work WAN-off per SC-002)
- Client: Windows 10 1809+ / 11 (WinUI 3 desktop, Windows App SDK 1.5), 4GB RAM thin, LAN/VPN to workstation

## 1. Clone & Build

```powershell
git clone <repo> raggit; cd raggit
dotnet build RAGGit.sln   # no extra workload: WinUI 3 client builds on Windows with the .NET 8 SDK alone
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

Configure `RAGGit.Workstation.Api/appsettings.json` (v1.3.0 — local per-person accounts are the primary credential; API keys remain for bootstrap; `VectorDb:VectorSize` 384|768 validated at startup per FR-001; legacy `Qdrant:Path` deprecated, warns if disagreeing):

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

## 3. Provision the first people (1.3.0 Operator CLI)

On the workstation, seed the first Admin and Employee before the API is running. The operator CLI writes directly to `data/rag.db` and never stores a plaintext password in configuration.

```powershell
dotnet run --project src/RAGGit.Workstation.Api -- user add --username ada --display-name "Ada Lovelace" --role Admin --password-stdin
dotnet run --project src/RAGGit.Workstation.Api -- user add --username bob --display-name "Bob Moore" --role Employee --password-stdin
```

## 4. Run Workstation API

```powershell
dotnet run --project src/RAGGit.Workstation.Api --urls https://0.0.0.0:5001
# Swagger https://ai-workstation.local:5001/swagger
# Health: GET https://ai-workstation.local:5001/health → 200 {vectorDb: ok, llm: ok, version:"1.3.0"}
# Bootstrap auth: GET /api/auth/me -H "X-Api-Key: <key>" → 200 {identityType:"ApiKey", role:"Admin"|"Employee"}
# Per-person auth: POST /api/auth/login (HTTPS) → {access_token, token_type:"Bearer", expires_in:28800}
```

## 5. Run Client (Thin, No Models — WinUI 3, v1.3.0)

```powershell
# Windows 10 1809+ / 11 desktop (unpackaged Debug F5 loop)
dotnet run --project src/RAGGit.Client.WinUI -p:Platform=x64
# Client reads src/RAGGit.Client.WinUI/appsettings.json (Workstation:Url).
# If a cached session exists it restores it; otherwise LoginPage prompts for username/password over HTTPS.
# Role is discovered via GET /api/auth/me at launch (FR-003/FR-004/FR-008).


# Publish Release (signed MSIX sideload per Constitution VII)
dotnet publish src/RAGGit.Client.WinUI -c Release -p:Platform=x64 -p:WindowsPackageType=MSIX   # Windows 10 1809+ / 11 → MSIX

# Remote Windows desktops reach the workstation over site VPN or the same LAN subnet — no cloud relay (Constitution IV)
```

Client flows: **Admin**: `Library → Upload (PDF/docx/txt/md <100MB)` → status `Indexing→Ready`; **Employee**: `Query → "what is refund policy?"` → `{answer, citations[]}`.

## 6. Verify Offline Invariant (SC-002)

```powershell
# On workstation, disable WAN (keep LAN):
# Windows: netsh interface set interface "Ethernet" admin=disable (WAN adapter) or firewall block 0.0.0.0/0
# Then from desktop on same LAN:
$token = (curl -s -X POST https://ai-workstation.local:5001/api/auth/login -H "Content-Type: application/json" -d '{"username":"bob","password":"<bob-pw>"}' | ConvertFrom-Json).access_token
curl https://ai-workstation.local:5001/api/documents -H "Authorization: Bearer $token"  # 200
curl -X POST https://ai-workstation.local:5001/api/query -H "Authorization: Bearer $token" -H "Content-Type: application/json" -d '{"query":"refund policy"}'
# → 200 {answer, citations} with no WAN — proves FR-004
```

## 7. Tests (v1.3.0 — opt-in real gate)

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
# Identity lifecycle (1.3.0):
dotnet test --filter "FullyQualifiedName~SessionLifecycleTests|FullyQualifiedName~AuthRefreshContractTests|FullyQualifiedName~IdentityAttributionTests|FullyQualifiedName~DeactivationRefusalTests"
# Full validation at once:
./scripts/validate-quickstart.ps1
dotnet csharpier check .
```

## Env Secrets (no hardcoding)

```powershell
# Bootstrap API keys are optional in 1.3.0; per-person accounts are provisioned below.
dotnet user-secrets set "Api:AdminKey" "<admin-key>" --project src/RAGGit.Workstation.Api
dotnet user-secrets set "Api:EmployeeKey" "<employee-key>" --project src/RAGGit.Workstation.Api
dotnet user-secrets set "Onnx:EmbeddingModelPath" "./models/bge-micro-v2/onnx/model.onnx" --project src/RAGGit.Workstation.Api
```

See [SECURITY.md](../../../SECURITY.md) for the single-tenant, signing, and
data-exclusion policy.
