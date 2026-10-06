# Implementation Plan: Client UX Optimization

**Branch**: `029-client-ux-optimization` | **Date**: 2026-10-05 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/029-client-ux-optimization/spec.md`, including the
Session 2026-10-05 clarifications: account actions surface through the details panel via an
auto-highlighted first match; search results must appear within a tenth of a second; dialogs place
the confirming action first and the dismissing action last.

## Summary

An experience pass over the desktop client's data surfaces, following the consistency polish that
feature 028 shipped. Five workstreams:

1. **Record search** on the Library, History, and My Documents — client-side filtering over records
   already loaded, with match counts, a distinct no-match empty state, and session-persistent
   search text (FR-001–004).
2. **Dashboard wayfinding** — the failure metric distinguished by glyph shape and text rather than
   color alone, em-dash placeholders during the first load, and a visible destination cue on every
   metric card (FR-005–007).
3. **Dense tables at narrow widths** — a pinned, sortable header for the People directory and one
   more tier of column collapse on both tables at 720 DIPs (FR-008–010).
4. **Fewer admin steps** — the People directory auto-highlights the first search match so the
   existing details panel offers account actions without a row click, and every dialog adopts one
   action order (FR-011–012).
5. **Progress text** — plain-language lines during sign-in and answer preparation, and long answers
   arriving with their opening in view (FR-013–014).

Presentation-only per FR-017. The only behavior-layer additions are a `SearchSessionState` singleton
(search text per surface, so it survives transient-page navigation), the derived filtered views on
three existing ViewModels, and the auto-highlight rule in the People directory's code-behind. No
API, contract, storage, or retrieval change; every frozen automation identifier is preserved.

**Acceptance bar, per surface** (resolves the spec's tension between "no paging or scrolling" and
"search only sees loaded records" — see [research.md](research.md) R1): the Library loads its full
list, so search alone finds any document. History and My Documents load incrementally, so search
finds any record within the loaded set and the existing "Load more" extends that set; their match
caption states the loaded count, never a server total.

## Technical Context

**Language/Version**: C# / XAML, .NET 10 (`net10.0-windows10.0.17763.0`)

**Primary Dependencies**: WPF-UI `4.3.0` (already referenced; no new dependency). Verified for this
feature:

- The library ships **no `DataGrid` and no skeleton/shimmer control**, so the pinned People-directory
  header reuses the Library's pinned-header pattern (a header row sibling to the `ListView`) and the
  Dashboard placeholders are em-dash `TextBlock`s bound to `HasLoaded` (research R4, R8).
- The `ContentDialog` footer lays out Primary, then Secondary, then Close left-to-right (verified
  against the 4.3.0 control template), so `WpfDialogService.ConfirmAsync` — `PrimaryButtonText =
  "Yes"`, `CloseButtonText = "No"` — already satisfies the confirming-first order, and every caller
  (delete document, role change, enable/disable, sign-out) inherits compliance (research R7).
- `LibraryViewModel`, `HistoryViewModel`, and `DocumentsMineViewModel` are `AddTransient`, as are
  their pages (`App.xaml.cs:225-245`), and navigation creates a fresh page per visit — so a
  ViewModel property cannot survive leaving a surface. Session scope for search text therefore comes
  from a new `SearchSessionState` singleton, following the existing `AskNavigationState` seam that
  was introduced for exactly this lifetime gap (research R3).
- The Library fetches its full list (`DocumentsApiClient.GetDocumentsAsync`, no paging parameters)
  and slices it in memory via `LibraryPage.Create`; History and My Documents page at 20 through
  `GetHistoryAsync(limit, offset)` with `LoadMore` appending. Filtering therefore sees everything on
  the Library and only the loaded set elsewhere (research R1).
- The Dashboard's Failed metric already swaps its glyph color at zero (`DashboardPage.xaml:345-364`),
  and `ChatControl` already shows "Preparing answer…" and auto-scrolls
  (`ChatControl.xaml:261-269`, `ChatControl.xaml.cs:137-148`) — both are extended, not replaced
  (research R8, R9).

**Storage**: N/A — presentation only; no table, preference, DTO, API, or contract change.

**Testing**: Full build `dotnet build RAGGit.sln -c Release -p:Platform=x64`; existing
unit/contract/offline integration suites green with zero assertion changes, **plus** new test-first
unit tests for the behavior-layer additions (search filtering, paging over the filtered set,
filter re-application after `LoadMore` and refresh, auto-highlight of the first match); `dotnet
csharpier check .` (XAML included); the static design audits extended per
[quickstart.md](quickstart.md); a `winapp ui` walkthrough of all 22 scenarios.

**Target Platform**: Windows 10 1809+ / Windows 11 desktop (single-tenant, offline-first)

**Project Type**: Desktop application (WPF client)

**Performance Goals**: Search results reflect the typed text within a tenth of a second (SC-002).
Met structurally — filtering is a synchronous in-memory scan with no debounce — so the bound is
validated by walkthrough rather than a flaky timed test (research R2). No other measurable budget;
no request-volume change.

**Constraints**: Presentation-only per FR-017 — `RAGGit.Client.Core` additions limited to the
`SearchSessionState` seam and derived filtered views; zero new endpoints or request-shape changes;
match captions must not imply
a server-wide total where only a page is loaded (U1.4); placeholders never contain a digit (U2.4);
attention styling never renders over an error state (U2.3); confirming action first, dismissing
action last, in every dialog (U5); zero hard-coded colours, zero `Opacity=` emphasis, only valid
`SymbolRegular`/`ThemeResource` keys (023 rules hold); existing automation IDs preserved (FR-015);
light/dark/high-contrast legible at 800×600 (FR-016); offline invariant untouched.

**Scale/Scope**: Three ViewModels gain `SearchText` and a filtered view; `AdminUsersPage` code-behind
gains the auto-highlight rule; XAML changes to the Library, History, My Documents, Dashboard, People
directory, Login, and chat surfaces plus two dialogs. No new project, no new shared component.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Single-Tenant On-Prem**: PASS — no tenant, data-model, or deployment change.
- **II. Workstation-Owned AI**: PASS — no model, vector-store, or network behaviour change. Search
  filters records already fetched; no new request is introduced.
- **III. .NET Library-First & Client Reuse**: PASS — presentation layer plus a session-state seam
  and derived views in the shared behavior layer, which the spec's Assumptions explicitly permit
  ("minimum wiring a new affordance needs"); no new project, no duplicated logic.
- **IV. Offline Invariant (NON-NEGOTIABLE)**: PASS — no query-time path touched; filtering is local
  over loaded data; all assets local.
- **V. Citation-Grounded RAG**: PASS — no retrieval or citation semantics. The only chat change is
  scroll alignment so a long answer's opening is visible.
- **VI. Test-First (NON-NEGOTIABLE)**: PASS — the new behavior-layer logic (filtering, filtered
  paging, auto-highlight) is unit-testable and written test-first (red → green); existing suites are
  the regression gate with zero assertion changes; visual contracts are proven by the static audits
  and the `winapp ui` walkthrough.
- **VII. Simplicity & Proprietary Stewardship**: PASS — one project, no dependency change. The pinned
  header and placeholders reuse patterns the client already contains rather than adding controls.

**Pre-Phase-0 gate**: PASS. No violation requires Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/029-client-ux-optimization/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output (transient state only)
├── quickstart.md        # Phase 1 output (validation guide)
├── contracts/           # Phase 1 output (UI contracts U1–U6)
│   └── ui-contracts.md
└── tasks.md             # Phase 2 output (/speckit.tasks — not created here)
```

### Source Code (repository root)

```text
src/RAGGit.Client.Core/
├── Services/
│   └── SearchSessionState.cs        # NEW singleton — per-surface search text, survives navigation
└── ViewModels/
    ├── LibraryViewModel.cs          # seeds SearchText from session state; pager slices filtered list
    ├── HistoryViewModel.cs          # seeds SearchText from session state; filtered view over loaded items
    └── DocumentsMineViewModel.cs    # seeds SearchText from session state; filtered view over loaded items

src/RAGGit.Client.WPF/
└── Views/
    ├── Pages/
    │   ├── LibraryPage.xaml / .cs   # search box, match caption, no-match state; Type/Size collapse ≤720
    │   ├── HistoryPage.xaml         # search box, match caption, no-match state
    │   ├── DocumentsMinePage.xaml   # search box, match caption, no-match state
    │   ├── DashboardPage.xaml       # attention glyph+caption, em-dash placeholders, destination captions
    │   ├── AdminUsersPage.xaml / .cs # pinned header row; column collapse ≤720; auto-highlight first match
    │   └── LoginPage.xaml           # "Signing in…" progress line
    ├── Dialogs/
    │   ├── CreatePersonDialog.xaml  # committing action declared before Cancel
    │   └── ResetPasswordDialog.xaml # committing action declared before Cancel
    └── Components/
        └── ChatControl.xaml / .cs   # progress line wording; scroll aligns to the top of the newest message

tests/unit/                          # NEW tests for filtering, filtered paging, auto-highlight (written first)
tests/contract|integration/          # existing suites — regression gate only
```

**Structure Decision**: Keep the single-solution layout. No new shared component is justified —
search boxes are three small, surface-specific blocks, and the pinned header is a per-page
arrangement (the Library already proved it). The only behavior-layer changes are the search state
the spec's Assumptions allow.

## Complexity Tracking

No constitutional violations require additional complexity justification.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — (none) | — | — |

**Post-Phase-1 gate**: PASS — presentation plus one session-state seam and derived views; no new
project, dependency,
storage, API, or contract change; offline and citation invariants untouched. The Phase 1 contracts
introduce no workstation-facing surface (`contracts/api.yaml` is intentionally absent), and the data
model is entirely transient.
