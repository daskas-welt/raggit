# AGENTS.md — RAGGit

Private single-tenant RAG: ASP.NET workstation API + WPF desktop client (WPF-UI). SDK pinned by
`global.json` (10.0.401); `Directory.Build.props` sets `net10.0`, `Nullable`, `ImplicitUsings`
(`TreatWarningsAsErrors=false`, so build warnings are benign).

## Commands

- `dotnet tool restore` → `dotnet csharpier check .` (CSharpier 1.3.0, `.config/dotnet-tools.json`).
  It formats **XAML as well as C#** — run `dotnet csharpier format .` after touching `.xaml`.
- Full solution (Windows only, includes the WPF client): `dotnet build RAGGit.sln -c Release -p:Platform=x64`.
  Without the client: `dotnet build RAGGit.Server.slnf -c Release` — that filter is what Linux CI builds.
- Test projects are `tests/unit|contract|integration/RAGGit.Tests.*.csproj`. Filter examples:
  `--filter "FullyQualifiedName~Tests.Unit"`; the offline subset is
  `QueryOfflineTests|OfflineIdentityTests|OfflineHistoryTests|CrossUserIsolationTests`; "remaining
  integration" is `FullyQualifiedName~Tests.Integration` minus those four. Add `-c Release --no-build`
  for repeat runs — a running API or open VS locks `bin/Debug` DLLs (MSB3026/MSB3021).
- `RequiresOllama` tests skip when Ollama is absent; plain `dotnet test` must stay green without it.
- **Background ingest (in progress):** uploads return `201` with `DocumentStatus.Uploading` and are
  indexed by `IngestWorker`; the work queue marks the document `Queued` when it is published, so the
  persisted lifecycle is `Uploading → Queued → Indexing → Ready|Failed`. Anything that uploads then
  asserts on status/search must wait for the worker (see `IntegrationTestFactory.WaitForSettledAsync`)
  instead of `Task.Delay`. Failures land on `DocumentStatus.Failed` with a user-safe
  `Document.FailureReason`; re-uploading the same bytes re-stages a `Failed` document (retry) but
  leaves `Ready`/in-flight ones untouched. Cell-cap and no-extractable-content uploads are no longer
  `413`/`400` — they are accepted and fail in the background, so `specs/001…005/contracts/api.yaml`
  (400-corrupt, status enum) is stale for them.
- Unit 339 / contract 86 / integration 62 pass on 2026-09-29 (Release, Ollama up). Confirm before
  treating a failure as pre-existing.
- CI (`.github/workflows/ci.yml`) triggers only on `main` and the hard-coded branches `001-*`…`005-*`
  and `010-winui-client`; a new `specs/NNN-*` branch gets no CI.

## Data / config gotchas

- `VectorDb:VectorSize` must be 384|768|1024 and the embed model must match (`all-minilm`/`bge-micro`
  →384, `nomic-embed`→768, `bge-m3`/`arctic-embed2`/`mxbai`→1024) or startup logs a mismatch warning.
  Dev ships 1024 + `snowflake-arctic-embed2`. Changing the size against an existing collection throws
  `DimensionMismatchException` — wipe `src/RAGGit.Workstation.Api/data/lancedb` and re-ingest.
- **Tests do not use that path.** Each test project resolves `./data/lancedb` relative to its *output*
  dir: `tests/<suite>/bin/<Config>/net10.0/data/lancedb`. Deleting the repo-root `data/` fixes nothing;
  delete the test-output one.
- `tests/*/bin/**/data` also holds the test SQLite DB and JWT key — they are regenerated, so wiping the
  directory is safe when tests fail with stale state.
- Create a local login without the UI: run the API exe with the operator CLI **from
  `src/RAGGit.Workstation.Api`** (the DB path is CWD-relative):
  `.\bin\Release\net10.0\RAGGit.Workstation.Api.exe user add --username me --display-name Me --role Admin --password 'Passw0rd!'`.
- The client treats a config with no API key as invalid (`ClientConfigResolver`): `Workstation:Url`
  alone falls back to an `invalid-config` client, and login shows `cannot reach AI workstation`. Add
  `Workstation:ApiKey` (or `Api:AdminKey`/`Api:EmployeeKey`) to `src/RAGGit.Client.WPF/appsettings.json`;
  it is copied to the output dir on build.
- API ports from `Properties/launchSettings.json`: http `5142`, https `5001`; the client's default
  `Workstation:Url` is `https://localhost:5001`.

## Architecture (not obvious from names)

- The desktop client is **WPF** (`src/RAGGit.Client.WPF`, + `WPF-UI` / `WPF-UI.DependencyInjection`
  4.3.0; docs via Context7 `/lepoco/wpfui`). `src/RAGGit.Client.WinUI`, `src/RAGGit.Client.Maui` and
  the constitution's "WinUI 3" wording are superseded — do not resurrect them or create files there.
- `RAGGit.Client.Core` still declares the **`RAGGit.Client.Maui.*` namespaces**
  (`RAGGit.Client.Maui`, `.Services`, `.ViewModels`, `.Config`) — 26 files. That is intentional; do not
  rename them.
- Flow: WPF pages/code-behind → `Client.Core` ViewModels (`CommunityToolkit.Mvvm`
  `[ObservableProperty]`/`[RelayCommand]` source-gen) + `*ApiClient` (HttpClient only) →
  `Workstation.Api` controllers → `RAGGit.Retrieval` (topK=5 + `MinScore`, grounded prompts) /
  `RAGGit.Ingest` (chunk 512/50). Storage: LanceDB `./data/lancedb` + SQLite `rag.db`.
- Invariants (`.specify/memory/constitution.md`): single-tenant (no `company_id` anywhere), no
  query-time WAN, answers carry `[chunk-GUID]` citations or exactly `no relevant content found`, never
  transfer facts between same-template records of different people. TDD is mandatory (VI).

## WPF-UI traps (compile fine, fail or render wrong at runtime)

- `{ui:ThemeResource X}` is **enum-keyed** — an unknown key throws `XamlParseException` when the page
  loads. `AccentFillColorDefaultBrush` is *not* a member (use `{DynamicResource AccentFillColorDefaultBrush}`);
  check new keys against `Wpf.Ui.Markup.ThemeResource`.
- `Symbol="…"` must be a real `SymbolRegular` member. `ChevronDoubleLeft24`, `Copy`, `Delete`,
  `Document`, `ArrowUpload` do **not** exist (valid: `ChevronDoubleLeft20`, `Copy24`, `Delete24`,
  `Document24`, `ArrowUpload24`).
- `Icon="…"` on `ui:Button` renders **nothing**; use
  `<ui:Button.Icon><ui:SymbolIcon Symbol="…" /></ui:Button.Icon>`.
- `Appearance` exists only on `ui:TextBlock` — not on `ui:SymbolIcon` or plain `TextBlock`.
- No `Opacity=` to dim text (breaks High Contrast); use the theme tokens. Keep all colour token-based.
- Keep `AutomationProperties.AutomationId` values stable — 22 IDs are frozen across features 022/023 —
  and add IDs for new interactive elements.

## Verifying UI

- `winapp ui` (WinApp CLI) drives the app over UI Automation; the repo ships
  `.opencode/skills/winui-ui-testing`. Known limits: the nav `AutoSuggestBox` exposes as a *List* (its
  text is not drivable), `ui:Card`/`Border` automation IDs are not surfaced, layered popups cannot be
  screenshotted, and `click` targets coordinates while `invoke` uses patterns (survives layout changes).
- A client pointed at a live API is the only way to see signed-in surfaces; start the API first, then
  patch the client's output `appsettings.json` (see the ApiKey gotcha above) — don't patch the source
  file if you don't intend to keep the change.

## Feature work

- Spec Kit drives features: `.opencode/commands/speckit.*` (`/speckit.specify → plan → tasks →
  implement`), helpers in `.specify/scripts/powershell/`, and `.specify/feature.json` holding the
  current feature directory. `/speckit.implement` reads whichever feature that file points at — keep it
  pointed at the feature you mean.
- Layout per feature: `specs/NNN-name/{spec.md,plan.md,tasks.md,research.md,quickstart.md}` plus
  `checklists/requirements.md` and `contracts/` (`api.yaml` for server features, `ui-contracts.md` for
  client ones); older features also carry `verification.md`.
- `.opencode/agents/{coder,spec-planner}.md` define the repo's subagents.

<!-- CODEGRAPH_START -->
## CodeGraph

In repositories indexed by CodeGraph (a `.codegraph/` directory exists at the repo root), reach for it BEFORE grep/find or reading files when you need to understand or locate code:

- **MCP tool** (when available): `codegraph_explore` answers most code questions in one call — the relevant symbols' verbatim source plus the call paths between them, including dynamic-dispatch hops grep can't follow. Name a file or symbol in the query to read its current line-numbered source. If it's listed but deferred, load it by name via tool search.
- **Shell** (always works): `codegraph explore "<symbol names or question>"` prints the same output.

If there is no `.codegraph/` directory, skip CodeGraph entirely — indexing is the user's decision.
<!-- CODEGRAPH_END -->
