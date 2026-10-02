# Implementation Plan: All-Pages UX/UI Polish

**Branch**: `028-pages-ux-polish` | **Date**: 2026-10-02 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/028-pages-ux-polish/spec.md` (incl. Session 2026-10-02
clarifications: Dashboard adopts the shared header; per-surface pagination kept; form dialogs go
in-window while Upload stays a window with aligned chrome; in-control busy wins app-wide).

## Summary

A consistency-and-feedback pass over every desktop-client page and dialog: one header treatment
(including Settings and Dashboard), one severity-styled status idiom with retry on every
asynchronously loaded page, one busy rule (in-control, content stays visible), transient snackbar
confirmations for copy/download/sign-out outcomes, chat flow fixes (auto-scroll, in-stream busy,
reliable "Ask again", clear conversation), responsive table degradation at the minimum window size,
44-DIP row-action targets, and Login/Settings parity.

Presentation-only per FR-023, with small affordance seams in `RAGGit.Client.Core` where a new
affordance needs wiring (notification seam, pending-ask state, clear-conversation command,
per-download busy, connection re-check). No API, contract, storage, or retrieval change; every
frozen automation identifier is preserved.

## Technical Context

**Language/Version**: C# / XAML, .NET 10 (`net10.0-windows10.0.17763.0`)

**Primary Dependencies**: WPF-UI `4.3.0` (already referenced). Verified for this feature:
`ISnackbarService.Show(title, message, ControlAppearance, IconElement?, TimeSpan)` exists and its
presenter is already wired in the shell (`RootSnackbarPresenter`, `MainWindow.xaml.cs:39`) but is
never called today; `IContentDialogService` + `RootContentDialogHost` (`MainWindow.xaml:59`) already
host `WpfDialogService.ConfirmAsync`, which any in-window page can reach via
`App.Services.GetRequiredService<IDialogService>()` (used by Library/Admin today); `ui:InfoBar`
severity presentation exists on Dashboard; the in-button icon→`ui:ProgressRing` morph exists on the
Dashboard refresh control; `AuthApiClient.GetAuthMeAsync()` (`GET /api/auth/me`) exists for the
Settings re-check; the Core-seam pattern (`IDialogService`/`ILauncherService`/`IFilePicker`:
interface in `Client.Core`, `Wpf*` adapter in the WPF project, singleton registration) is the
template for the new notification seam; `QueryDetailNavigationState` (DI singleton, written by the
navigator, read-and-cleared by the target page's `Loaded`) is the template for the pending-ask
state.

**Storage**: N/A — presentation only; no table, preference, DTO, API, or contract change.

**Testing**: Full build `dotnet build RAGGit.sln -c Release -p:Platform=x64`; existing
unit/contract/offline integration suites green with zero assertion changes, **plus** new
test-first unit tests for the new Core affordance seams (notification seam with in-memory double,
pending-ask state, clear-conversation command, per-download busy gating); `dotnet csharpier
check .` (XAML included); the static design audits extended in [quickstart.md](quickstart.md);
`winapp ui` walkthrough of every affected surface.

**Target Platform**: Windows 10 1809+ / Windows 11 desktop (single-tenant, offline-first)

**Project Type**: Desktop application (WPF client)

**Performance Goals**: No measurable regression; conversation auto-scroll must stay responsive in
10+ exchange conversations; snackbar confirmations never block or displace page content.

**Constraints**: Presentation-only per FR-023 — `RAGGit.Client.Core` additions limited to
affordance seams; **zero `MessageBox.Show`** in the client (FR-009); status text wraps, never
trims (FR-015); zero hard-coded colours, zero `Opacity=` emphasis, only valid
`SymbolRegular`/`ThemeResource` keys (023 rules hold); existing automation IDs preserved (FR-022);
light/dark/high-contrast legible at 800×600 (FR-024); refresh affordance and busy idiom identical
everywhere (FR-004/FR-005); offline invariant untouched (no request-path change).

**Scale/Scope**: `App.xaml` (shared Thickness resources + header/status styles), `Components/`
(ChatControl auto-scroll + one shared status-footer component), `Services/` (new
`WpfNotificationService`), all 9 pages, the 3 dialogs, and four `Client.Core` touchpoints
(`INotificationService` + double, pending-ask state, `QueryViewModel` clear command + in-stream
busy, `LibraryViewModel` per-download busy + role-aware empty state). `MainWindow` needs no change
(both hosts are already wired).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Single-Tenant On-Prem**: PASS — no tenant, data-model, or deployment change.
- **II. Workstation-Owned AI**: PASS — no model, vector-store, or network behaviour change.
- **III. .NET Library-First & Client Reuse**: PASS — presentation layer plus documented affordance
  seams in the shared behavior layer (spec Assumptions allow extending bindings/commands where a
  new affordance needs wiring); no new project.
- **IV. Offline Invariant (NON-NEGOTIABLE)**: PASS — no query-time path touched; the Settings
  re-check reuses the existing `GET /api/auth/me` call already made at startup; all assets local.
- **V. Citation-Grounded RAG**: PASS — no retrieval or citation semantics; the QueryDetail
  citations empty state gains the standard glyph treatment only.
- **VI. Test-First (NON-NEGOTIABLE)**: PASS — the new Core seams are unit-testable and are written
  test-first (red → green); existing suites are the regression gate with zero assertion changes;
  visual contracts are proven by the static audits + `winapp ui` walkthrough.
- **VII. Simplicity & Proprietary Stewardship**: PASS — one project, no dependency change; shared
  styles/components replace duplicated per-page markup instead of adding machinery.

**Pre-Phase-0 gate**: PASS. No violation requires Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/028-pages-ux-polish/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output (transient state + new seams)
├── quickstart.md        # Phase 1 output (validation guide)
├── contracts/           # Phase 1 output (UI contracts U1–U8)
│   └── ui-contracts.md
└── tasks.md             # Phase 2 output (/speckit.tasks — not created here)
```

### Source Code (repository root)

```text
src/RAGGit.Client.Core/
├── Services/
│   ├── INotificationService.cs          # NEW seam (+ in-memory double for tests)
│   └── AskNavigationState.cs            # NEW pending-prompt singleton (QueryDetailNavigationState pattern)
└── ViewModels/
    ├── QueryViewModel.cs                # ClearConversation command; in-stream busy hook
    ├── LibraryViewModel.cs              # per-download busy; role-aware empty-state copy
    └── DocumentsMineViewModel.cs        # (unchanged — load-more stays)

src/RAGGit.Client.WPF/
├── App.xaml                             # shared Thickness resources (4px scale) + PageHeaderCard/status styles
├── Components/
│   ├── ChatControl.xaml / .cs           # auto-scroll, copy feedback, in-stream busy
│   └── StatusFooterControl.xaml / .cs   # NEW shared InfoBar-based status idiom + optional retry
├── Services/
│   └── WpfNotificationService.cs        # NEW — wraps the wired ISnackbarService
├── Views/
│   ├── MainWindow.xaml                  # unchanged (SnackbarPresenter + ContentDialogHost already wired)
│   ├── Pages/
│   │   ├── DashboardPage.xaml           # shared header card; profile card into content row
│   │   ├── LibraryPage.xaml / .cs       # status footer, 44-DIP row buttons, responsive columns, empty-state copy
│   │   ├── QueryPage.xaml               # shared header (+Clear action), remove page-covering ring
│   │   ├── QueryDetailPage.xaml         # section styles, citations empty state, copy feedback
│   │   ├── HistoryPage.xaml             # retry, shared footer, copy feedback, ask-again via state
│   │   ├── DocumentsMinePage.xaml       # retry, shared footer
│   │   ├── AdminUsersPage.xaml          # InfoBar status, retry, Fluent-first cleanup
│   │   ├── SettingsPage.xaml / .cs      # header card, re-check action, copyable values, sign-out confirm
│   │   └── LoginPage.xaml / .cs         # Enter submit, shared busy/error idioms
│   └── Dialogs/
│       ├── UploadDialog.xaml / .cs       # aligned window chrome, cancel confirm
│       ├── CreatePersonDialog.*         # → in-window ContentDialog
│       └── ResetPasswordDialog.*       # → in-window ContentDialog; MessageBox removed

tests/unit/                               # NEW tests for the Core seams (written first)
tests/contract|integration/               # existing suites — regression gate only
```

**Structure Decision**: Keep the single-solution layout. Repeated treatments (header card, status
footer, spacing) become shared styles/components instead of per-page markup; the only behavior-layer
additions are the four affordance seams the spec's Assumptions allow.

## Complexity Tracking

No constitutional violations require additional complexity justification.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — (none) | — | — |

**Post-Phase-1 gate**: PASS — presentation plus affordance seams only; no new project, dependency,
storage, API, or contract change; offline and citation invariants untouched.
