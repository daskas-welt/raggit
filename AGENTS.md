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
- **Two-level chunks (030):** ingest persists child chunks (512/50, embedded, vector payload
  carries `parentId`) plus parent chunks (groups of `Ingest:ParentGroupSize` = 4 children,
  not embedded) in `Chunks(Level, ParentId)`; broad queries resolve parents at retrieval time.
  `ReindexBackfillService` runs once per startup and re-enqueues `Ready` documents with no parent
  row through the normal background path (idempotent), so after upgrading, expect stale documents
  to cycle `Queued → Indexing → Ready` on first boot. `POST /api/queries` accepts optional
  `mode: auto|broad|specific` (default `auto`) and echoes the effective `mode: broad|granular`.
- Unit 549 / contract 102 / integration 83 pass on 2026-10-16 (Release, Ollama up); the offline subset is
  12/12. Confirm before treating a failure as pre-existing.
- CI (`.github/workflows/ci.yml`) triggers on `main` and any specs branch matching `[0-9][0-9][0-9]-*`,
  so a new `specs/NNN-*` feature gets the build, unit/contract, offline and windows-client jobs without
  editing the workflow.

## Data / config gotchas

- `VectorDb:VectorSize` must be 384|768|1024 and the embed model must match (`all-minilm`/`bge-micro`
  →384, `nomic-embed`→768, `bge-m3`/`arctic-embed2`/`mxbai`→1024) or startup logs a mismatch warning.
  Dev ships 1024 + `snowflake-arctic-embed2`. Changing the size against an existing collection throws
  `DimensionMismatchException` — wipe `src/RAGGit.Workstation.Api/data/lancedb` and re-ingest.
- A test factory that builds the **real** Ollama clients must pass the app's `Ollama:TimeoutMs`
  (300000 ms). `OllamaEmbedder`/`OllamaLlmClient` default to 5000 ms, which a large ingest batch exceeds,
  so the document lands on `Failed` ("Document ingestion failed") instead of `Ready` — the cause of
  `RealLoopTests` being red long after the loop itself worked.
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

- The desktop client is **WPF** (`src/RAGGit.Client.WPF`, using `WPF-UI` / `WPF-UI.DependencyInjection`
  4.3.0; upstream: https://github.com/lepoco/wpfui; site: https://wpfui.lepo.co/; documentation:
  https://wpfui.lepo.co/documentation/; docs via Context7 `/lepoco/wpfui`).
  `src/RAGGit.Client.WinUI` and the constitution's "WinUI 3" wording are superseded — do not resurrect
  them or create files there.
- **Use Fluent (WPF-UI) controls only.** Compose every surface from `ui:*` controls — `ui:Card`,
  `ui:CardControl`, `ui:CardAction`, `ui:Button`, `ui:TextBlock`, `ui:InfoBar`, `ui:Flyout`,
  `ui:SymbolIcon`, `ui:ProgressRing`, … — not stock WPF chrome (`Button`, `GroupBox`, `TabControl`,
  `Expander`, stock `TextBox`, …) where a Fluent equivalent exists. Stock WPF stays for layout only
  (`Grid`, `WrapPanel`, `DockPanel`, `ScrollViewer`) and where WPF-UI exposes no equivalent.
- Stay on **WPF-UI (lepoco)**; do **not** migrate to the built-in WPF Fluent theme (.NET 9+). That
  theme is still experimental/in progress, only restyles stock WPF controls, and supplies none of the
  `ui:*` controls or theme tokens used here — running both would compete for stock-control priority.
  `WPF Gallery` (https://apps.microsoft.com/detail/9ndlx60wx4kq; source
  https://github.com/microsoft/WPF-Samples/tree/main/Sample%20Applications/WPFGallery, MIT) is a
  reference only, not a dependency.
- Shared client code uses the `RAGGit.Client.Core.*` namespaces (`.Services`, `.ViewModels`, `.Config`);
  keep those framework-independent roots when adding or moving client behavior.
- Flow: WPF pages/code-behind → `Client.Core` ViewModels (`CommunityToolkit.Mvvm`
  `[ObservableProperty]`/`[RelayCommand]` source-gen) + `*ApiClient` (HttpClient only) →
  `Workstation.Api` controllers → `RAGGit.Retrieval` (topK=5 + `MinScore`, grounded prompts) /
  `RAGGit.Ingest` (chunk 512/50). Storage: LanceDB `./data/lancedb` + SQLite `rag.db`.
- **Live reasoning (032):** the Ask surface consumes `POST /api/queries/stream`, which answers with
  newline-delimited JSON (`application/x-ndjson`): zero or more `progress` events
  (`understanding|searching|sources|composing`, each carrying a workstation-rendered sentence in the
  question's language, with `sourceCount`/`documentNames` only on `sources`) followed by exactly one
  terminal `answer` or `error` event. Both transports run the same `RAGGit.Retrieval.QueryPipeline`
  (the pipeline the old `QueryController.Post` inlined); `POST /api/queries` passes a null observer and
  is byte-for-byte unchanged. **Reasoning is transient** — never persisted, never in history, never part
  of a `ChatMessage`; the old static busy line is replaced by the `ReasoningProgressList` region. The
  health probe and model validation finish before the first byte, so `400/401/403/503` stay ordinary
  JSON; a failure after that arrives as an in-band `error` event whose message matches the classic
  endpoint's, and a stream that ends without a terminal event is treated as a failure by the client.
- Invariants (`.specify/memory/constitution.md`): single-tenant (no `company_id` anywhere), no
  query-time WAN, answers carry `[chunk-GUID]` citations or exactly `no relevant content found`, never
  transfer facts between same-template records of different people. TDD is mandatory (VI).

## WPF-UI traps (compile fine, fail or render wrong at runtime)

- `{ui:ThemeResource X}` is **enum-keyed** — an unknown key throws `XamlParseException` when the page
  loads. `AccentFillColorDefaultBrush` is *not* a member (use `{DynamicResource AccentFillColorDefaultBrush}`);
  check new keys against `Wpf.Ui.Markup.ThemeResource`.
- **Never assign an `ObjectDataProvider` straight to `ItemsSource`.** `ItemsSource="{StaticResource X}"`
  where `X` is an `ObjectDataProvider` (e.g. an `x:Type`/`GetValues` enum source) throws
  `ArgumentException: 'ObjectDataProvider' is not a valid value for property 'ItemsSource'` at page
  load. Bind *through* it: `ItemsSource="{Binding Source={StaticResource X}}"`.
- **A page whose XAML throws during construction fails silently.** `NavigationView` swallows the
  `XamlParseException` from the page constructor and `App.DispatcherUnhandledException` sets
  `Handled = true`, so clicking the nav item just does nothing — no dialog, no crash. Every unhandled
  exception is appended to `%LOCALAPPDATA%\RAGGit\crash.log`; read that first when a page "won't open".
- `Symbol="…"` must be a real `SymbolRegular` member. `ChevronDoubleLeft24`, `Copy`, `Delete`,
  `Document`, `ArrowUpload` do **not** exist (valid: `ChevronDoubleLeft20`, `Copy24`, `Delete24`,
  `Document24`, `ArrowUpload24`).
- `Icon="…"` on `ui:Button` renders **nothing**; use
  `<ui:Button.Icon><ui:SymbolIcon Symbol="…" /></ui:Button.Icon>`.
- `Appearance` exists only on `ui:TextBlock` — not on `ui:SymbolIcon` or plain `TextBlock`.
- No `Opacity=` to dim text (breaks High Contrast); use the theme tokens. Keep all colour token-based.
- Keep `AutomationProperties.AutomationId` values stable — 22 IDs are frozen across features 022/023 —
  and add IDs for new interactive elements.

## XAML layout preference

- **Space with Grid tracks, not `Margin`.** No client XAML uses a layout `Margin`; don't add one.
  The only remaining margins are item spacing inside an `ItemsControl`/`UniformGrid` panel
  (`ItemContainerStyle` or a `DataTemplate` root) and direct children of a `UniformGrid`, because
  those panels have no tracks to gutter; comment them when you touch one.
  Declare explicit `Grid.ColumnDefinitions`/`Grid.RowDefinitions` and interleave fixed-spacing
  gutter tracks with content tracks (`*,12,*,12,…` / `Auto,8,Auto,8,…`); place item *n* at
  `Grid.Column = (n % columns) * 2` and `Grid.Row = (n / columns) * 2`, so gutters need no margins.
  Reflow by zeroing unused content/gutter tracks (`new GridLength(0)`) from page-owned code-behind —
  reuse `ResponsiveGridLayout.ColumnsFor` for the breakpoints. Inner card padding (icon inset, text
  inset) is fixed rows/columns too. Page-level vertical insets (`PagePadding`, 24) are outer gutter
  rows — or `ScrollViewer.Padding` where the page is one — and dialog insets are outer gutter
  rows/columns. A single child with a four-sided inset gets a small wrapper Grid whose edge tracks
  carry the inset. `NavigationView.FrameMargin` is a WPF-UI property, not a Thickness margin, and
  may stay at `0`.
- **Declare tracks as property elements, not shorthand attributes.** Write
  `<Grid.RowDefinitions>`/`<Grid.ColumnDefinitions>` with explicit `<RowDefinition>`/
  `<ColumnDefinition>` children instead of `RowDefinitions="Auto, 12, Auto"` /
  `ColumnDefinitions="*, 24, Auto"`. Every track then sits on its own line where it can be read,
  reordered, or reflowed from code-behind without decoding a comma-separated string.
- Every client page and component follows this pattern — the whole client is margin-free with
  property-element track definitions. The Dashboard page (`DashboardPage.xaml`, reflowed from
  `ApplyResponsiveLayout`) is the reference implementation; follow it for new surfaces.

## Verifying UI

- `winapp ui` (WinApp CLI) drives the app over UI Automation; the repo ships
  `.opencode/skills/winui-ui-testing`. Known limits: the nav `AutoSuggestBox` exposes as a *List* (its
  text is not drivable), `ui:Card`/`Border` automation IDs are not surfaced, layered popups cannot be
  screenshotted, and `click` targets coordinates while `invoke` uses patterns (survives layout changes).
- A client pointed at a live API is the only way to see signed-in surfaces; start the API first, then
  patch the client's output `appsettings.json` (see the ApiKey gotcha above) — don't patch the source
  file if you don't intend to keep the change.

## Deployment (031)

- Two modes, one artifact set: **local** (workstation on a machine on the company network) and
  **private cloud** (company-controlled VM reachable only from the company's own network — never an
  open public endpoint). Selected by configuration only; see `deploy/` and
  `docs/runbooks/workstation-deployment.md`.
- `deploy/docker-compose.prod.yml` is the TLS stack for **both** modes:
  `docker compose --env-file deploy/.env -f deploy/docker-compose.prod.yml up -d`. The repository-root
  `docker-compose.yml` is the *developer* compose — plain HTTP on 5001, which a signed-in client
  cannot use because the client refuses non-HTTPS sign-in.
- `deploy/.env` fails fast on missing values: `BIND_ADDRESS`, `RAGGIT_DATA_DIR`, both API keys, and
  the Kestrel PFX path/password. `.env` is gitignored; only `.env.example` is committed. In the
  deployed private-cloud design `BIND_ADDRESS=0.0.0.0` is used **because the cloud firewall has zero
  inbound rules** — access is over a Tailscale tailnet, so there is no public `443` or `22` at all.
  `WORKSTATION_HOST` is the tailnet MagicDNS name so the certificate's **DNS SAN** matches; clients
  must use that name, not the raw tailnet IP.
- Validate the stack after any compose edit:
  `docker compose --env-file deploy/.env.example -f deploy/docker-compose.prod.yml config`.
- Client config template: `deploy/appsettings.client.template.json` (URL **and** key — see the ApiKey
  gotcha above). TLS trust comes from the workstation certificate that `deploy/bootstrap.sh` writes
  next to the PFX (`…/certs/raggit.crt`).

## Feature work

- Spec Kit drives features: `.opencode/commands/speckit.*` (`/speckit.specify → plan → tasks →
  implement`), helpers in `.specify/scripts/powershell/`, and `.specify/feature.json` holding the
  current feature directory. `/speckit.implement` reads whichever feature that file points at — keep it
  pointed at the feature you mean.
- Layout per feature: `specs/NNN-name/{spec.md,plan.md,tasks.md,research.md,quickstart.md}` plus
  `checklists/requirements.md` and `contracts/` (`api.yaml` for server features, `ui-contracts.md` for
  client ones); older features also carry `verification.md`.
- `.opencode/agents/{coder,architect}.md` define the repo's subagents.

<!-- CODEGRAPH_START -->
## CodeGraph

In repositories indexed by CodeGraph (a `.codegraph/` directory exists at the repo root), reach for it BEFORE grep/find or reading files when you need to understand or locate code:

- **MCP tool** (when available): `codegraph_explore` answers most code questions in one call — the relevant symbols' verbatim source plus the call paths between them, including dynamic-dispatch hops grep can't follow. Name a file or symbol in the query to read its current line-numbered source. If it's listed but deferred, load it by name via tool search.
- **Shell** (always works): `codegraph explore "<symbol names or question>"` prints the same output.

If there is no `.codegraph/` directory, skip CodeGraph entirely — indexing is the user's decision.
<!-- CODEGRAPH_END -->
