# RAGGit — Offline-Mode Single-Tenant RAG Library

> **Proprietary, on-prem, single-tenant RAG for companies.** An AI Workstation hosts the local vector store (`LanceDB` file) and local LLM (`Ollama`/`LLamaSharp`/`ONNX`) and serves a thin `WinUI 3` desktop client over LAN — no cloud egress at query time.

**Constitution**: `v1.2.0` ratified `2026-08-31`, last amended `2026-09-13` — `Single-Tenant On-Prem`, `Workstation-Owned AI`, `.NET Library-First & Client Reuse`, `Offline Invariant (NON-NEGOTIABLE)`, `Citation-Grounded RAG`, `Test-First`, `Simplicity & Proprietary` — see [`.specify/memory/constitution.md`](.specify/memory/constitution.md).

**Version**: `1.4.0` (API `specs/005-per-person-history/contracts/api.yaml` MINOR additive — adds `GET /api/queries/history`, `GET /api/queries/{id}`, `GET /api/documents/mine` scoped to JWT `sub`, zero cross-user leak, legacy `admin`/`employee` excluded, stable pagination, offline LAN-only; no breaking change. `1.3.0` added local per-person accounts: `POST /api/auth/login`, `POST /api/auth/refresh`, `GET /api/auth/me` additive envelope, `/api/users` Admin CRUD, HTTPS-only credentials, PBKDF2 + JWT 8h, lockout 5/15m; `1.2.0` added `xlsx` MIME + cap; `1.1.0` added `GET /api/auth/me` + corrupted-pdf 400). `/health` reports `version:1.4.0`.

**Features**: `001-offline-mode` — [spec](./specs/001-offline-mode/spec.md) | `002-real-bringup` — [spec](./specs/002-real-bringup/spec.md) | [plan](./specs/002-real-bringup/plan.md) | [verification](./specs/002-real-bringup/verification.md) | `003-ingest-breadth` — [spec](./specs/003-ingest-breadth/spec.md) | [plan](./specs/003-ingest-breadth/plan.md) | [verification](./specs/003-ingest-breadth/verification.md) | `004-identity` — [spec](./specs/004-identity/spec.md) | [arch](./specs/004-identity/docs/architecture.html?theme=light) | [workflow](./specs/004-identity/docs/workflow.html?theme=light) | [sequence](./specs/004-identity/docs/sequence.html?theme=light) | [dataflow](./specs/004-identity/docs/dataflow.html?theme=light) | `005-per-person-history` — [spec](./specs/005-per-person-history/spec.md) | [plan](./specs/005-per-person-history/plan.md) | [verification](./specs/005-per-person-history/verification.md) | [contract](./specs/005-per-person-history/contracts/api.yaml) | `006-client-architecture` — [spec](./specs/006-client-architecture/spec.md) | [plan](./specs/006-client-architecture/plan.md) | [tasks](./specs/006-client-architecture/tasks.md)

## Architecture

> Interactive diagrams — **click image for interactive HTML (`?theme=light`)**. GitHub blob view sanitizes HTML; use **raw.githack** to render, or download.

[![004-Identity Architecture — showcase light](docs/004-identity/architecture.png)](https://raw.githack.com/daskas-welt/raggit/main/docs/004-identity/architecture.html?theme=light)
*Architecture — showcase `216fad62` `c850aa23` `811kB` — Workstation API + JWT + Users + LanceDB + Ollama + MAUI + CLI — [interactive `?theme=light`](https://raw.githack.com/daskas-welt/raggit/main/specs/004-identity/docs/architecture.html?theme=light) · [spec source](specs/004-identity/docs/architecture.html?theme=light) · [docs mirror](docs/004-identity/architecture.html?theme=light)*

[![004-Identity Workflow — showcase light](docs/004-identity/workflow.png)](https://raw.githack.com/daskas-welt/raggit/main/docs/004-identity/workflow.html?theme=light)
*Workflow — `94965aa9` `1aca0cba` `811kB` — Provision → Login → Use → Manage — [interactive](https://raw.githack.com/daskas-welt/raggit/main/specs/004-identity/docs/workflow.html?theme=light)*

[![004-Identity Sequence — showcase light](docs/004-identity/sequence.png)](https://raw.githack.com/daskas-welt/raggit/main/docs/004-identity/sequence.html?theme=light)
*Sequence — `bfb222bc` `e5b3a0ec` `812kB` — Login → Bearer → Admin — [interactive](https://raw.githack.com/daskas-welt/raggit/main/specs/004-identity/docs/sequence.html?theme=light)*

[![004-Identity Dataflow — showcase light](docs/004-identity/dataflow.png)](https://raw.githack.com/daskas-welt/raggit/main/docs/004-identity/dataflow.html?theme=light)
*Dataflow — `0e58f73e` `45a85f70` `807kB` — Person → Token → Attribution lineage — [interactive](https://raw.githack.com/daskas-welt/raggit/main/specs/004-identity/docs/dataflow.html?theme=light)*

> Lifecycle is conditional for 004 and deferred (account states are linear; lifecycle diagram will be added if a retry/failure branch is introduced)
> Legacy showcase: **[docs/raggit.html?theme=light](docs/raggit.html?theme=light)** (`10d002…`) · [raw.githack](https://raw.githack.com/daskas-welt/raggit/main/docs/raggit.html?theme=light) · In-repo HTML at `specs/004-identity/docs/` (view → Raw → download)

```
Company LAN (no WAN at query time) — see architecture diagram above for interactive topology
AI Workstation (on-prem) ── LAN ── Employee Desktops (WinUI 3 thin clients, Win10 1809+ / Win11)
├─ ASP.NET Core API (src/RAGGit.Workstation.Api) v1.4.0 — local per-person accounts + per-person history/mine ─────┐
├─ LanceDB file data/lancedb (VectorDb:Path, VectorDb:VectorSize 384|768)                 │
├─ Ollama localhost:11434 (all-minilm 384 dev / nomic-embed-text 768 prod)                │
├─ SQLite rag.db — Users (PBKDF2-SHA256, role, lockout 5/15m, 8h JWT, HTTPS) + Documents/Queries ──┘
WinUI Client: net8.0-windows10.0.17763.0 (Windows App SDK 1.5, single-project MSIX) — Credential-Locker token cache + Bearer handler + LoginPage + Admin users grid + Library + ChatControl, no vendor license, no MAUI workload
Operator CLI: `raggit user add` — OS trust anchor, no first-run wizard, no plaintext secret in config
API 1.4.0: POST /api/auth/login (HTTPS), POST /api/auth/refresh, GET /api/auth/me {identityType:"Local", role, displayName, username, sub}, /api/users CRUD (Admin), 401/403/429, offline invariant + per-person history GET /api/queries/history?limit=&offset=, GET /api/queries/{id} (404 if not owned), GET /api/documents/mine (additive, no breaking change)
```

**Dev on one machine**: workstation = `localhost:5001` + `localhost:11434`, desktop → `localhost:5001` (same machine, still LAN-only). See [Quickstart](#quickstart-single-machine-dev).

## Tech Stack

- **Language**: C# .NET 8 (`RAGGit.Core`/`Ingest`/`Retrieval`/`Workstation.Api`/`Client.Core`/`Client.WinUI`)
- **Vector**: `LanceDB` .NET SDK embedded `connect(VectorDb:Path)` (file, no server; `VectorDb:VectorSize` 384|768 validated at startup, dimension guard fails fast with `delete data/lancedb` recovery).
- **AI**: `OllamaSharp` (`POST /api/embed` + `/api/chat` + `TimeoutMs 5000` offline fail-fast → 503 `model unavailable offline`) or `LLamaSharp` GGUF + `ONNX Runtime` (`bge-micro-v2` + `Phi-3-mini`) behind non-default `Ingest:Embedder` flag (FR-005)
- **Client**: `WinUI 3` desktop app `net8.0-windows10.0.17763.0` (Windows App SDK 1.5, Windows 10 1809+ / 11, x64/x86/ARM64) over a shared `net8.0` behavior library `RAGGit.Client.Core` (ViewModels/services/session/config — unit-tested). UI is stock WinUI 3 controls + `CommunityToolkit.Mvvm`, no vendor license key, no MAUI workload. Debug runs unpackaged/self-contained (`-p:Platform=x64`); Release packages as single-project MSIX (`Package.appxmanifest`, signed at release). No hard-coded URLs/keys — `Workstation:Url` + `Workstation:ApiKey` via `appsettings.json` + `GET /api/auth/me` role gating.
- **Testing**: xUnit + FluentAssertions, WAN-disabled integration suite, opt-in real suite `dotnet test --filter RequiresOllama` (skips gracefully when `Ollama:Url` absent per SC-003), 50 Q/A eval harness, `measureIngestPerformance.ps1` SC-001 gate

## Project Structure

```
RAGGit.sln (v1.4.0)
├── src/
│   ├── RAGGit.Core/              # Models Document/Chunk/Query, abstractions IVectorStore/IEmbedder/ILlmClient
│   ├── RAGGit.Ingest/            # Chunker 512/50 → embed (Ollama) → LanceDB upsert (CorruptDocumentException → 400)
│   ├── RAGGit.Retrieval/         # embed query → LanceDB search topK=5 → prompt → local LLM (timeout → 503)
│   ├── RAGGit.Workstation.Api/   # ASP.NET Core: /api/documents (+/mine 1.4.0), /api/query, /api/queries/history + /{id} (1.4.0), /api/auth/me, /health
│   ├── RAGGit.Client.Core/       # net8.0 shared client behavior (ViewModels/services/session/config) — unit-testable, no UI framework
│   └── RAGGit.Client.WinUI/      # WinUI 3 shell (Windows-only) — XAML pages, components, converters, adapters, Package.appxmanifest
├── tests/
│   ├── unit/ | contract/ | integration/  # WAN-disabled offline suite + opt-in RequiresOllama + SC-001 perf gate + SC-005 corruption
│   ├── fixtures/                 # sample-50pages.pdf (50 pages, refund policy) + bad.pdf / bad.docx (FR-006)
├── specs/001-offline-mode/       # spec.md, plan.md, quickstart.md (synced to 1.1.0), contracts/api.yaml 1.1.0
├── specs/002-real-bringup/       # spec.md, plan.md, research.md, data-model.md, quickstart.md, verification.md, contracts/api.yaml
├── scripts/                      # validate-quickstart.ps1, measureIngestPerformance.ps1
├── data/                         # .gitignored: data/lancedb (VectorDb:Path), rag.db
└── models/                       # .gitignored: *.gguf, *.onnx
```

## Quickstart — Single-Machine Dev (v1.4.0)

No workstation needed. Everything runs on `localhost`. Identity is now local per-person accounts; API keys remain valid for machine/bootstrap.

### Prereqs

- .NET 8 SDK (`dotnet --version` ≥8.0), Git LFS for ONNX (optional)
- 16GB RAM recommended (8GB works with `all-minilm` + `phi-3-mini` 384d), 10GB disk
- A dev HTTPS certificate: `dotnet dev-certs https --trust` (single-machine dev only); production uses an internal-CA or distributed self-signed workstation certificate

### 1. Build

```powershell
git clone https://github.com/daskas-welt/raggit.git; cd raggit
dotnet build RAGGit.sln   # Windows: full solution incl. WinUI client (Any CPU maps WinUI→x64)
# Linux/CI (no Windows App SDK): dotnet build RAGGit.Server.slnf
dotnet csharpier check .   # formatted per a9c7cb4 (CI enforces)
```

### 2. AI Models (once, then WAN can be off)

```powershell
ollama serve
# Dev laptop baseline (RAM-friendly, 384d):
ollama pull all-minilm
ollama pull phi3:mini
# Prod workstation baseline (separate box, 768d):
# ollama pull nomic-embed-text
# ollama pull llama3.2:3b
```

`src/RAGGit.Workstation.Api/appsettings.json` (dev defaults — `VectorSize` must match `EmbedModel` per FR-001):
```json
{
  "VectorDb": { "Path": "./data/lancedb", "Provider": "LanceDB", "VectorSize": 384 },
  "Ollama": { "Url": "http://localhost:11434", "EmbedModel": "all-minilm", "ChatModel": "phi3:mini", "TimeoutMs": 5000 }
}
```
Prod block in `appsettings.Development.json` comments `nomic-embed-text`/`768`/`llama3.2:3b`.

```powershell
# Bootstrap API keys are optional in 1.3.0; per-person accounts are the primary credential.
dotnet user-secrets set "Api:AdminKey" "dev-admin-key" --project src/RAGGit.Workstation.Api
dotnet user-secrets set "Api:EmployeeKey" "dev-employee-key" --project src/RAGGit.Workstation.Api
```

### 3. Provision the first people (1.3.0 Operator CLI)

On the workstation, use the operator CLI to seed the first accounts before anyone signs in. The command writes directly to `data/rag.db` and does not need the API to be running.

```powershell
dotnet run --project src/RAGGit.Workstation.Api -- user add --username ada --display-name "Ada Lovelace" --role Admin --password-stdin
dotnet run --project src/RAGGit.Workstation.Api -- user add --username bob --display-name "Bob Moore" --role Employee --password-stdin
```

### 4. Run (2 terminals)

```powershell
# Terminal A — workstation (your "AI workstation")
Remove-Item -Recurse -Force ./data/lancedb -ErrorAction SilentlyContinue  # fresh DB after dimension swap
dotnet run --project src/RAGGit.Workstation.Api --urls https://localhost:5001
# Swagger https://localhost:5001/swagger  Health https://localhost:5001/health → {vectorDb:ok, llm:ok, version:"1.4.0"}
# Bootstrap auth: curl -s https://localhost:5001/api/auth/me -H "X-Api-Key: dev-admin-key" → {identityType:"ApiKey", role:"Admin"}
# Per-person auth: curl -s https://localhost:5001/api/auth/login -H "Content-Type: application/json" -d '{"username":"bob","password":"<bob-pw>"}' → {access_token, token_type:"Bearer", expires_in:28800}

# Terminal B — WinUI client (Windows 10 1809+ / 11, x64). Debug is unpackaged +
# self-contained, so no installed App Runtime or workload is required:
# One-time: the client refuses to start without an ApiKey configured (fail-fast
# config-error screen, no HTTP attempted). Any non-empty value works for local
# dev — sign-in itself uses the per-person username/password:
dotnet user-secrets set "Workstation:ApiKey" "dev-local-key" --project src/RAGGit.Client.WinUI
dotnet build src/RAGGit.Client.WinUI/RAGGit.Client.WinUI.csproj -c Debug -p:Platform=x64
.\src\RAGGit.Client.WinUI\bin\x64\Debug\net8.0-windows10.0.17763.0\RAGGit.Client.WinUI.exe
# (or F5 the RAGGit.Client.WinUI project in Visual Studio)
```

Client reads `src/RAGGit.Client.WinUI/appsettings.json` (`Workstation:Url`). If a cached session exists (Credential Locker) it restores it; otherwise the `LoginPage` prompts for username/password over HTTPS. Role is discovered from `GET /api/auth/me` (FR-003/FR-004/FR-008). No hard-coded URLs/keys per `ClientConfigTests`.

### Distribute the desktop client (MSIX sideload)

Release builds package as single-project MSIX (`src/RAGGit.Client.WinUI/Package.appxmanifest`, identity `DaskasWelt.RAGGit`, Win10 1809+ floor). Placeholder tile art lives in `Assets/` — replace with company art before release.

```powershell
# 1. One-time per machine: trust the company signing cert (dev: self-signed CN=RAGGit Dev)
#    Admin shell: certutil -addstore Root raggit-dev.cer
#    Create it (matches the manifest Publisher CN=RAGGit Dev):
$pw = ConvertTo-SecureString "…" -Force -AsPlainText
New-SelfSignedCertificate -Type Custom -Subject "CN=RAGGit Dev" -KeyUsage DigitalSignature `
  -CertStoreLocation "Cert:\CurrentUser\My" -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")

# 2. Build + sign (CI uploads the unsigned package as an artifact; sign at release):
dotnet publish src/RAGGit.Client.WinUI -c Release -p:Platform=x64 `
  -p:AppxPackageSigningEnabled=false -p:GenerateAppxPackageOnBuild=true
signtool sign /fd SHA256 /f raggit-dev.pfx /p "…" *.msix

# 3. Double-click the .msix on Win10 1809+ / Win11 to install (no extra runtime step).
```

### 5. Verify Offline Invariant + Corruption Hardening

```powershell
# Sign in as the Admin you provisioned and capture the Bearer token:
$token = (curl -s -X POST https://localhost:5001/api/auth/login -H "Content-Type: application/json" -d '{"username":"ada","password":"<ada-pw>"}' | ConvertFrom-Json).access_token

curl -X POST https://localhost:5001/api/documents -H "Authorization: Bearer $token" -F "file=@sample.pdf"
curl https://localhost:5001/api/documents -H "Authorization: Bearer $token"  # → [{status:Ready}] <5 min SC-001
# Corrupted pdf (FR-006 SC-005):
curl -X POST https://localhost:5001/api/documents -H "Authorization: Bearer $token" -F "file=@bad.pdf"
# → 400 {error:"corrupted pdf"}  # no partial index; GET /api/documents does not list it
# Disable WiFi (keep localhost), then:
curl -X POST https://localhost:5001/api/query -H "Authorization: Bearer $token" -H "Content-Type: application/json" -d '{"query":"refund policy"}'
# → {answer, citations:[{documentId, chunkId, text}]} or no relevant content found
# With Ollama stopped → 503 {error:"model unavailable offline"} within TimeoutMs 5000, never hang (FR-007)
```

### 6. Tests

```powershell
dotnet test                    # fakes only, no Ollama — CI must stay green (SC-003)
dotnet test --filter "RequiresOllama"   # opt-in real loop (needs ollama serve + all-minilm/phi3:mini + fresh data/lancedb)
pwsh ./scripts/measureIngestPerformance.ps1  # SC-001 perf gate: 50-page PDF <300s (fake <4s)
./scripts/validate-quickstart.ps1       # build + unit + contract + integration
dotnet csharpier check .        # formatting gate (CI)
```

## Specs — 001-offline-mode / 002 / 003

- **US-1 P1**: Admin upload → index Ready <5min for 50 pages (001) + xlsx multi-sheet Ready <30s (003 SC-001)
- **US-2 P1**: Employee WAN-off query → cited answer <7s p95 or `no relevant content found` + xlsx hidden never returned (003 SC-002) + formula cached 42.50 (SC-003)
- **US-3 P2**: Admin delete purges vectors
- **US-4 P3**: Employee browse read-only (403 on write)
- **FR-010**: pdf/docx/xlsx/txt/md — xlsx added in 1.2.0 (003) with 100k cell cap, hidden sheets skipped, header-repeat, cached formulas (video/audio still out of scope)
- **FR-011**: 5k docs / ~1M chunks, 512/50, topK=5 (xlsx 100k cap is per-doc guard orthogonal to 100MB)
- **007-library-pagination**: Document Library pages client-side (no server/contract change) — default 25 rows, options 10/25/50/100 persisted locally under `RAGGit.LibraryPageSize`; see [spec](./specs/007-library-pagination/spec.md)
- **008-upload-sheet**: Upload is a modal side sheet over the Library reusing `UploadViewModel` (with cancel); the dedicated Upload view/route/flyout item are deleted; see [spec](./specs/008-upload-sheet/spec.md)
- **009-library-item-actions**: Library rows show Type / Size (MB) / Status / Creator (raw `CreatedBy`, display name saved at upload); per-row ⤓ downloads the stored original via `GET /api/documents/{id}/content` and opens it externally (legacy docs: "Original unavailable"); per-row 🗑 is Admin-only with confirm and purges the stored original too; see [spec](./specs/009-library-item-actions/spec.md)

## Development Workflow

Constitution-driven SDD: `constitution` → `specify` → `clarify` → `plan` → `tasks` → `implement`. `tasks.md` has 43 tasks phased as `Setup (T001-T006) → Foundational (T007-T013 BLOCKS) → US-1 (T014-T021) → US-2 (T022-T029 MVP) → US-3/4 → Polish`.

```powershell
# In .specify scripts (PowerShell)
.specify/scripts/powershell/create-new-feature.ps1 -ShortName "offline-mode" "..."
.specify/scripts/powershell/setup-plan.ps1
.specify/scripts/powershell/setup-tasks.ps1
```

## License

Proprietary — all rights reserved. Models stay on customer-owned workstations, not redistributed.

---

**Next**: `dotnet new` scaffolding per `tasks.md T001` → implement `Foundational` → `US-1` MVP ingest half → `US-2` query half (closed-loop shippable).
