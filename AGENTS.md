# AGENTS.md — RAGGit

Private single-tenant RAG: ASP.NET workstation API + WinUI 3 desktop client. .NET 10 SDK pinned (`global.json`); `Nullable` + `ImplicitUsings` on everywhere (`Directory.Build.props`).

## Build / test (exact commands matter)

- Formatter gate runs in CI: `dotnet tool restore`, then `dotnet csharpier check .`. Format before pushing.
- Linux/CI never builds WinUI: use the filter `dotnet build RAGGit.Server.slnf -c Release`. Full `RAGGit.sln` + MSIX packaging is Windows-only (`-p:Platform=x64`).
- Test suites by filter: `dotnet test --filter "FullyQualifiedName~Tests.Unit"`, `...~Tests.Contract`, offline subset `QueryOfflineTests|OfflineIdentityTests|OfflineHistoryTests|CrossUserIsolationTests`, rest is `Tests.Integration`. Plain `dotnet test` must stay green **without Ollama** (`RequiresOllama` tests skip).
- Local gotcha: a running API or open VS locks `bin/Debug` DLLs and the build fails with MSB3026/3021. Use `-c Release` for local builds/tests, or stop the API first.
- `data/lancedb` dimension guard: changing `VectorDb:VectorSize` (384|768|1024) against an existing collection throws `DimensionMismatchException` at startup/first request. Fix is wipe `./data/lancedb` + re-ingest. Local dev default is 1024 (bge-m3, Greek libraries); align `Ollama:EmbedModel` ↔ size.

## Architecture (not obvious from names)

- `src/RAGGit.Client.Maui/` is a dead directory (only `obj/`). The live `RAGGit.Client.Maui.Services/ViewModels` namespaces live in **`src/RAGGit.Client.Core`** — do not "fix" the naming or create files under `Client.Maui`.
- Flow: WinUI (`RAGGit.Client.WinUI`, XAML + code-behind) → thin `Client.Core` ViewModels (`CommunityToolkit.Mvvm` `[ObservableProperty]`/`[RelayCommand]` source-gen) + `*ApiClient` (`HttpClient` only) → `RAGGit.Workstation.Api` controllers → `RAGGit.Retrieval` (`RetrievalService` topK=5 + `MinScore`, `GenerationService` grounded prompts) / `RAGGit.Ingest` (chunk 512/50). Storage: LanceDB `./data/lancedb` + SQLite `rag.db`.
- Invariants (constitution `.specify/memory/constitution.md`): single-tenant (no `company_id` anywhere), no query-time WAN, answers carry `[chunk-GUID]` citations or exactly `no relevant content found` — never hallucinate, never transfer facts across same-template records of different people.
- OpenAPI contracts live per-feature in `specs/*/contracts/api.yaml`; feature work follows `specs/NNN-name/{spec.md,plan.md,tasks.md,research.md,quickstart.md,verification.md}`. TDD is mandatory.

## WinUI work

- Repo skills in `.opencode/skills/`: load `winui-dev-workflow` before building/running the client, `winui-code-review` before committing UI changes, `winui-design` before new XAML.
- UI tests use `AutomationProperties.AutomationId` — keep existing IDs (`SuggestionChips`, `AskAgainButton`, `CopyMessageButton`, …) stable and add IDs for new interactive elements.
