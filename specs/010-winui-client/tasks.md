# Tasks: WinUI 3 Client (Replace MAUI)

**Input**: Design documents from `/specs/010-winui-client/` (spec.md with US1–US4, plan.md with structure + decisions)

**Prerequisites**: plan.md (present), spec.md (present), constitution v1.2.0 (amendment to v1.3.0 ships in Phase 6)

**Tests**: Existing suites (`tests/unit`, `tests/contract`, `tests/integration`) MUST stay green unchanged — they are the regression gate for every phase. New adapter/page code follows test-first where unit-testable (seams already exist in Core).

**Organization**: Grouped by user story; each story independently testable. Scaffold (csproj, App, MainWindow, 5 adapters, LoginPage) already exists in the working tree — early tasks verify/complete it rather than recreate it.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: WinUI project registered, restorable, and correctly targeted

- [x] T001 Add `src/RAGGit.Client.WinUI/RAGGit.Client.WinUI.csproj` to `RAGGit.sln` and verify `dotnet restore` resolves WindowsAppSDK with no MAUI workload installed
- [x] T002 Verify TFM/packaging in `src/RAGGit.Client.WinUI/RAGGit.Client.WinUI.csproj` (`net8.0-windows10.0.17763.0`, `UseWinUI`, `WindowsPackageType=None` Debug / MSIX Release, x64/x86/ARM64)
- [x] T003 [P] Verify `src/RAGGit.Client.WinUI/appsettings.json` (`Workstation:Url`) copies to output and `src/RAGGit.Client.WinUI/app.manifest` (dpiAware, longPathAware, Win10 compat) is wired

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: DI shell + all five platform adapters compile; app launches

**⚠ CRITICAL**: No user story work can begin until this phase is complete

- [x] T004 Complete DI composition in `src/RAGGit.Client.WinUI/App.xaml.cs` (config resolve via `ClientConfigResolver`, `ClientSession`, token store, `ApiKey`/`Bearer` handlers, 5 HttpClients with 10min/150s timeouts, invalid-config inert path mirroring `MauiProgram.cs`)
- [x] T005 Complete shell in `src/RAGGit.Client.WinUI/MainWindow.xaml(.cs)` (NavigationView + Frame, config-error overlay, `DiscoverRoleAsync`, admin visibility, HWND capture for picker)
- [x] T006 [P] Verify Credential-Locker storage in `src/RAGGit.Client.WinUI/Services/WinUISecureStorage.cs` (Get/Set/Remove/RemoveAll over `PasswordVault`, empty-vault miss handling)
- [x] T007 [P] Verify HWND-initialized picker in `src/RAGGit.Client.WinUI/Services/WinUIFilePicker.cs` (`FileOpenPicker` filters pdf/docx/xlsx/txt/md, null on cancel, `OwnerHwnd` set from `MainWindow`)
- [x] T008 [P] Verify external open in `src/RAGGit.Client.WinUI/Services/WinUILauncherService.cs` (stage to `TemporaryFolder`, `LaunchFileAsync`, invalid-char sanitizing)
- [x] T009 [P] Verify page-size prefs in `src/RAGGit.Client.WinUI/Services/WinUILibraryPreferences.cs` (`LocalSettings`, 10/25/50/100 validation, 25 default)
- [x] T010 Verify expiry navigation in `src/RAGGit.Client.WinUI/Services/WinUISessionExpiryNavigator.cs` (`DispatcherQueue` → `NavigateToLogin`, no-op when already on login)

**Checkpoint**: `dotnet build RAGGit.sln` green; `dotnet test` (unit/contract/integration) green — foundation ready, stories can proceed in priority order

---

## Phase 3: User Story 1 — Sign in + library on WinUI (Priority: P1) ★ MVP

**Goal**: Person launches, signs in (HTTPS), reaches Library; cached session restores; bad config fails fast

**Independent Test**: Fresh launch → sign in as Admin/Employee → Library; relaunch → restored; corrupt URL → config-error screen, zero HTTP attempts

- [x] T011 [US1] Complete `src/RAGGit.Client.WinUI/Views/LoginPage.xaml(.cs)` bound to Core `LoginViewModel` (`Username`/`Password`/`LoginCommand`/`IsBusy`/`ErrorMessage`/`HasError` — note: property is `ErrorMessage`, not `StatusMessage`)
- [x] T012 [US1] Complete session restore in `src/RAGGit.Client.WinUI/MainWindow.xaml.cs` (`InitializeSessionAsync`: cached session → opportunistic refresh within 15 min → `DiscoverRoleAsync` → Library; else Login)
- [x] T013 [US1] Verify config-error path in `src/RAGGit.Client.WinUI/MainWindow.xaml` + `src/RAGGit.Client.WinUI/App.xaml.cs` (overlay shown, no HttpClient constructed with real URL, no login attempted)

**Checkpoint**: US1 independent test passes manually — MVP demonstrable (sign-in → Library → restore → config error)

---

## Phase 4: User Story 2 — All screens via side navigation (Priority: P1)

**Goal**: Behavior parity for Library, Ask, History, My Docs, Upload, Admin through `NavigationView`, all behavior from Core

**Independent Test**: Employee + Admin walkthrough of all six destinations; Admin hidden for Employee; two Ask questions alternate correctly with citations

- [x] T014 [P] [US2] Create `src/RAGGit.Client.WinUI/Views/LibraryPage.xaml(.cs)` (ListView bound to `LibraryViewModel`, status indicator, page-size footer, download/delete row actions)
- [x] T015 [P] [US2] Create `src/RAGGit.Client.WinUI/Views/QueryPage.xaml(.cs)` + chat control (bind `Messages`/`QueryText`/`AskCommand`/`Citations`/`StatusMessage`; replace MAUI `MultiBinding` citation subtitle with `IValueConverter` or preformatted property)
- [x] T016 [P] [US2] Create `src/RAGGit.Client.WinUI/Views/HistoryPage.xaml(.cs)` + `QueryDetailPage.xaml(.cs)` (bound to `HistoryViewModel`/`QueryDetailViewModel`)
- [x] T017 [P] [US2] Create `src/RAGGit.Client.WinUI/Views/DocumentsMinePage.xaml(.cs)` (bound to `DocumentsMineViewModel`)
- [x] T018 [P] [US2] Create `src/RAGGit.Client.WinUI/Views/UploadDialog.xaml(.cs)` (alias for UploadSheet) as `ContentDialog` reusing Core `UploadViewModel` with cancel (per 008-upload-sheet: no dedicated route/flyout item)
- [x] T019 [US2] Create `src/RAGGit.Client.WinUI/Views/AdminUsersPage.xaml(.cs)` (bound to `AdminUsersViewModel`) and enforce admin gating in `src/RAGGit.Client.WinUI/MainWindow.xaml.cs` (`Nav_SelectionChanged` + `RefreshAdminVisibility`)
- [x] T020 [US2] Port themes/converters to `src/RAGGit.Client.WinUI/` (Light/Dark `ResourceDictionary` via `ThemeDictionaries`, citation/type/size/creator converters replacing `Converters/DocumentDisplayConverters.cs`)

**Checkpoint**: US1 + US2 parity walkthrough passes; `dotnet test` still green with zero assertion changes (SC-001/SC-002)

---

## Phase 5: User Story 3 — Native platform integrations (Priority: P2)

**Goal**: Upload picker, external open, persisted page size, Credential-Locker sessions, 401 → sign-in all work natively

**Independent Test**: Upload via OS picker; download round-trips bytes and opens externally; page size survives restart; forced 401 returns to sign-in with cleared token

- [x] T021 [US3] Wire adapters into pages in `src/RAGGit.Client.WinUI/` (Upload uses `WinUIFilePicker`; library download uses `WinUILauncherService`; footer uses `WinUILibraryPreferences`; shell uses `WinUISessionExpiryNavigator`)
- [x] T022 [US3] Verify edge surfaces in WinUI pages (legacy "original unavailable", empty-list states, workstation-unavailable + certificate-trust errors, ≥20 ordered chat messages)

**Checkpoint**: US3 independent test passes; no token/secret in plain files or logs (FR-005)

---

## Phase 6: User Story 4 — MAUI gone; MSIX install (Priority: P2)

**Goal**: Single WinUI client; clean-checkout build without MAUI workload; signed MSIX sideload on Win10 1809 + Win11; docs/constitution updated

**Independent Test**: Clean machine (no MAUI workload) builds; Release MSIX installs on Win10 1809 + Win11 VMs and runs offline flows; `grep -ri maui` clean (excluding historical specs)

- [x] T023 [US4] Remove `RAGGit.Client.Maui` from `RAGGit.sln` and delete `src/RAGGit.Client.Maui/`
- [x] T024 [US4] Add Release MSIX packaging in `src/RAGGit.Client.WinUI/` (`Package.appxmanifest`, dev test certificate, CI Release job passing `/p:WindowsPackageType=MSIX`)
- [x] T025 [US4] Amend constitution `.specify/memory/constitution.md` v1.2.0 → v1.3.0 MINOR (III: MAUI → WinUI 3; VII: project list `RAGGit.Client.Maui/` → `RAGGit.Client.WinUI/`) and update `README.md` (arch diagram, quickstart run/package commands, drop `dotnet workload install maui`) plus CI/scripts
- [x] T026 [US4] Validate clean-checkout `dotnet build RAGGit.sln` (no MAUI workload) + `dotnet test` full suites + `dotnet csharpier check .`
- [ ] T027 [US4] Smoke-test Release MSIX sideload on Win10 1809 and Win11 (install with no extra runtime step, sign-in → Library → Ask → History) — DEFERRED 2026-09-17: no VMs available; procedure explained, runs on demand

**Checkpoint**: All four stories functional; SC-003/SC-005 satisfied

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Debt paydown and final consistency

- [ ] T028 [P] Optional namespace rename `RAGGit.Client.Maui.*` → `RAGGit.Client.Core.*`/`RAGGit.Client.*` across `src/RAGGit.Client.Core/`, `src/RAGGit.Client.WinUI/`, `tests/unit/` (only if churn is affordable; behavior-neutral)
- [x] T029 [P] Update `specs/010-winui-client/` with `checklists/requirements.md` validation record and refresh `docs/` screenshots/diagrams from MAUI to WinUI (done 2026-09-17: checklist created; `publish.md` rewritten to WinUI MSIX; `architecture.md`/`operator-cli.md`/`performance.md`/`001-quickstart.md` updated; no client screenshots exist in `docs/` — only 004-identity arch diagrams; `raggit-architecture.json/html` were legacy-generated and removed 2026-09-24, regen via PlantUML)
- [ ] T030 Run full validation: `dotnet build` + `dotnet test` + `csharpier` + quickstart offline-invariant flow against the WinUI client

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately (largely scaffolded; verify)
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **User Stories (Phases 3–6)**: All depend on Foundational completion; run sequentially P1 → P1 → P2 → P2 (same shell files; do not parallelize across stories)
- **Polish (Phase 7)**: Depends on all stories complete

### Within Each Phase

- Verify scaffolded files compile before extending them
- Converters/themes (T020) before pages that consume them if done sequentially; otherwise keep names in sync
- Cutover (T023) only after US1–US3 parity proven — never delete MAUI before WinUI works
- Docs/constitution (T025) with cutover so the repo is never inconsistent

### Parallel Opportunities

- T003 with T001–T002 (different files); T006–T009 in parallel (separate adapter files)
- T014–T018 in parallel (separate page files; T019–T020 need shell/theme names agreed first)
- T028–T029 in parallel (rename vs docs; coordinate to avoid conflicts)
- `dotnet test` suites can run in parallel with builds (separate processes)

---

## Parallel Example: Foundational adapters

```bash
# Launch adapter verifications together (separate files, no dependencies):
Task: "Verify WinUISecureStorage.cs (PasswordVault)"
Task: "Verify WinUIFilePicker.cs (FileOpenPicker + HWND)"
Task: "Verify WinUILauncherService.cs (stage + launch)"
Task: "Verify WinUILibraryPreferences.cs (LocalSettings)"
```

## Parallel Example: User Story 2 pages

```bash
# Launch page ports together (separate files, shared Core contracts):
Task: "Create LibraryPage.xaml(.cs)"
Task: "Create QueryPage.xaml(.cs) + chat control"
Task: "Create HistoryPage + QueryDetailPage"
Task: "Create DocumentsMinePage.xaml(.cs)"
Task: "Create UploadSheet.xaml(.cs)"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (verify scaffold restores/builds)
2. Complete Phase 2: Foundational (DI + shell + 5 adapters compile)
3. Complete Phase 3: User Story 1 (sign-in → Library → restore → config error)
4. **STOP and VALIDATE**: US1 independent test manually; `dotnet test` green
5. Deploy/demo MVP if ready (unpackaged Debug build suffices)

### Incremental Delivery

1. Setup + Foundational → shell launches to login
2. + US1 → sign-in works (MVP!)
3. + US2 → full parity walkthrough (shippable unpackaged)
4. + US3 → native integrations verified
5. + US4 → MAUI deleted, MSIX sideload on Win10/Win11
6. Polish → rename/docs/final validation

---

## Notes

- Exact file paths are in each task; new pages go under `src/RAGGit.Client.WinUI/Views/`, adapters under `src/RAGGit.Client.WinUI/Services/`
- Core (`src/RAGGit.Client.Core/`) and all test suites are READ-ONLY for behavior — WinUI-only changes except the optional T028 rename
- `LoginViewModel` surfaces auth errors via `ErrorMessage`/`HasError` (not `StatusMessage`) — T011 must bind accordingly
- WinUI has no `MultiBinding` — T015/T020 must use a converter or preformatted property
- Commit after each task or logical group; stop at any checkpoint to validate
