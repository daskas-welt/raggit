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

Configure `RAGGit.Workstation.Api/appsettings.json`:

```json
{
  "Qdrant": { "Path": "./data/qdrant" },
  "Ollama": { "Url": "http://localhost:11434", "EmbedModel": "nomic-embed-text", "ChatModel": "llama3.2:3b" },
  "Onnx": { "EmbeddingModelPath": "./models/bge-micro-v2/onnx/model.onnx", "ChatModelPath": "./models/phi-3-mini/cpu-int4" }
}
```

## 3. Run Workstation API

```powershell
dotnet run --project src/RAGGit.Workstation.Api --urls http://0.0.0.0:5001
# Swagger: http://ai-workstation.local:5001/swagger
# Health: GET http://ai-workstation.local:5001/health → 200 {qdrant: ok, llm: ok}
```

## 4. Run Client (Thin, No Models — .NET MAUI)

```powershell
# Windows 11 desktop (MAUI workload installed in step 1)
dotnet run --project src/RAGGit.Client.Maui -f net8.0-windows10.0.19041.0 -- --workstation http://ai-workstation.local:5001 --api-key <key>

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

## 6. Tests

```powershell
dotnet test --filter "Category=unit"
dotnet test --filter "Category=contract"
dotnet test --filter "Category=integration" # includes WAN-disabled suite (requires workstation running)
```

## Env Secrets (no hardcoding)

```powershell
dotnet user-secrets set "Api:Key" "<key>" --project src/RAGGit.Workstation.Api
dotnet user-secrets set "Onnx:EmbeddingModelPath" "./models/bge-micro-v2/onnx/model.onnx" --project src/RAGGit.Workstation.Api
```
