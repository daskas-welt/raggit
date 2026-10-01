# Implementation Plan: Fluent System Icons Across the Client

**Branch**: `025-fluent-system-icons` | **Date**: 2026-09-30 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/025-fluent-system-icons/spec.md`

## Summary

Apply the client's Fluent system icon vocabulary everywhere an icon earns its place. The client already
draws its navigation, dashboard tiles, empty states, status chips and a few action buttons from the
component library's Fluent icon set, but most action controls are text-only (refresh, load more, sign
in/out, create person, reset password, retry, upload, cancel, close, ask again, back, view, copy) and
several state surfaces show words or colour without a glyph (person enabled/locked, workstation
reachability, plain status and error messages).

This feature **extends** the design system that `023` established rather than replacing it: it adds the
missing concept→glyph entries, gives every qualifying action control its agreed glyph (labels retained),
pairs each state with a glyph alongside its colour token, and keeps icons on the documented size scale
with theme-token colour and accessible names. Every proposed name is verified against the component
library's `SymbolRegular` enum before use — the repository's standing trap is an icon name that compiles
but throws at load.

Presentation-only: no behaviour, API, contract, or `RAGGit.Client.Core` change; every existing
automation identifier is preserved, and no labelled control becomes icon-only (the `016` decision to
keep the admin toggles clearly labelled stands).

## Technical Context

**Language/Version**: C# / XAML, .NET 10 (`net10.0-windows10.0.17763.0`)

**Primary Dependencies**: WPF-UI `4.3.0` (already referenced — no new package). Verified for this
feature by reflection against `Wpf.Ui.dll`: `SymbolIcon` exposes `Symbol` (a `SymbolRegular` member) and
`Filled`, and takes its colour from `Foreground`; every icon name this feature introduces is a member of
the enum (the full proposed list is in [research.md](research.md)); the `{ui:ThemeResource X}` colour
keys the icons use already exist and are audited by `023`'s D2 check.

**Storage**: N/A — presentation and documentation only.

**Testing**: `dotnet build RAGGit.sln -c Release -p:Platform=x64`; the existing Unit/Contract/offline
suites (unchanged, regression gate only); `dotnet csharpier check .`; the static icon audits in
[quickstart.md](quickstart.md); `winapp ui` screenshots for the affected surfaces across Light/Dark/High
Contrast and at 800×600.

**Target Platform**: Windows 10 1809+ / Windows 11 desktop (single-tenant, offline-first)

**Project Type**: Desktop application (WPF client)

**Performance Goals**: No measurable regression; icons must not cause clipping or reflow at 800×600.

**Constraints**: Presentation-only; every icon name valid in `SymbolRegular`; sizes drawn from the
documented scale; zero hard-coded colour and no `Opacity=`; labelled controls keep their labels;
existing automation IDs preserved; `RAGGit.Client.Core`, APIs and all contracts read-only.

**Scale/Scope**: `App.xaml`, the pages, the upload dialog, `Components/ChatControl`, and the
design-system reference in `specs/023-design-system-refinement/design-system.md` (extended in place, as
`024` did for the brand colour).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Single-Tenant On-Prem**: PASS — no tenant, data, or deployment change.
- **II. Workstation-Owned AI**: PASS — no model, vector, or network behaviour; all glyphs ship with the
  component library and are already local.
- **III. .NET Library-First & Client Reuse**: PASS — presentation layer only; no new project, no new
  package.
- **IV. Offline Invariant**: PASS — no request path is touched; icons are local font glyphs.
- **V. Citation-Grounded RAG**: PASS — no retrieval, generation, or citation semantics change; the
  "Sources" disclosure gains a glyph while keeping its behaviour and content.
- **VI. Test-First**: PASS — there is no new library surface to unit-test. XAML compiles as a build gate;
  the verifiable obligations are icon-name validity, colour/opacity token use, size-scale adherence,
  automation-ID preservation and surface coverage, which the static audits in
  [quickstart.md](quickstart.md) prove with visual review for the rendered surfaces — the precedent
  `023` and `024` set for presentation work. Existing suites stay untouched with zero assertion changes.
- **VII. Simplicity & Proprietary Stewardship**: PASS — one project, no dependency change; the
  vocabulary is documented in the existing design-system reference rather than a new artifact, and the
  icons come from the library already shipped.

**Pre-Phase-0 gate**: PASS. No violation requires Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/025-fluent-system-icons/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (icon vocabulary + boundary decisions)
├── data-model.md        # Phase 1 output (concept/action/state model)
├── quickstart.md        # Phase 1 output (validation guide)
├── contracts/           # Phase 1 output
│   └── ui-contracts.md  # F1–F7 iconography contracts
└── tasks.md             # Phase 2 output (/speckit.tasks — NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/RAGGit.Client.WPF/
├── App.xaml                                  # no change: EmptyStateGlyph and the shared styles are unchanged
├── Views/
│   ├── MainWindow.xaml                        # navigation glyphs already present (verify only)
│   └── Pages/
│       ├── LoginPage.xaml                     # Sign in glyph
│       ├── QueryPage.xaml                     # suggestion chips stay text-only (clarification)
│       ├── QueryDetailPage.xaml               # Ask again, Back, Copy prompt/answer/citation, empty-citation state
│       ├── LibraryPage.xaml                   # Retry, footer error glyph
│       ├── HistoryPage.xaml                   # Refresh, View, Ask again, Load more, footer error glyph
│       ├── DocumentsMinePage.xaml             # Refresh, Load more, footer error glyph
│       ├── AdminUsersPage.xaml                # Refresh, role, active toggle, lock state, Create user, Reset password, status glyph
│       ├── SettingsPage.xaml(.cs)             # Sign out button, connection-status glyph
│       └── DashboardPage.xaml                 # already icon-bearing (verify only)
├── Views/Dialogs/UploadDialog.xaml            # Upload, Cancel, Close
├── Components/
│   ├── ChatControl.xaml                       # Sources disclosure glyph
│   ├── StatusChip.xaml                        # already icon-bearing (verify only)
│   └── PaginationFooterControl.xaml           # already icon-bearing (verify only)
├── Converters/                                # no change: state/severity glyphs are selected by XAML DataTriggers
└── ViewModels/MainWindowViewModel.cs          # navigation glyphs (unchanged)

specs/023-design-system-refinement/design-system.md   # Concepts list extended with the new entries (FR-010)

tests/  └── existing suites (unchanged; regression gate only)
```

**Structure Decision**: Keep the single-solution layout and the client-presentation scope. All icons
live in the presentation layer; the vocabulary is recorded in the design-system reference `023` owns,
extended in place exactly as `024` extended it for the brand colour. No new file type, project, or
dependency is introduced.

## Complexity Tracking

No constitutional violations require additional complexity justification.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — (none) | — | — |

**Post-Phase-1 gate**: PASS — presentation only; reuses the library's icon set, existing theme tokens
and existing shared styles, and adds no project, dependency, storage, or contract change.
