# Research: Admin Page Redesign

**Feature**: `016-admin-redesign` | **Date**: 2026-09-18 | **Plan**: [plan.md](plan.md)

No NEEDS CLARIFICATION remained after specification (the reference screenshot fixed the layout intent; 011/015 conventions fixed the control language). This file records the decisions taken and the alternatives rejected.

## R-01 — Status state in the shared ViewModel, mirroring 015

- **Decision**: Add `StatusMessage`/`StatusSeverity`/`HasStatus` plus `ClearStatus`/`SetSuccess`/`SetWarning`/`SetError` helpers to the existing admin ViewModel; keep `ErrorMessage` as a compat surface always matching `StatusMessage` on Warning/Error.
- **Rationale**: Same 015 `UploadViewModel` pattern — Core stays UI-framework-free (severity as string), the view maps it through the existing `StatusSeverityConverter`; every command now sets a success confirmation (spec FR-006 zero-silent-outcomes rule).
- **Alternatives considered**: View-local status in code-behind (rejected — untestable, duplicates per-command logic), removing `ErrorMessage` outright (rejected — would break existing test assertions for no benefit).

## R-02 — List owns the scroll; forms get a capped scroller

- **Decision**: Delete the outer `ScrollViewer`; the users `ListView` is the page's only collection scroller (`MinHeight="240"`); the two form cards sit in a `ScrollViewer` capped at `MaxHeight="380"` that never contains the list.
- **Rationale**: Restores virtualization for 100+ users (spec FR-002/SC-003) while keeping create/reset reachable on short windows.
- **Alternatives considered**: Keep outer `ScrollViewer` (rejected — the 011 FR-005 virtualization violation the redesign exists to fix), fixed page with no form scroll (rejected — forms unreachable on short/narrow windows).

## R-03 — Labeled row actions instead of glyph-only buttons

- **Decision**: Role = text `Button` ("Admin"/"Employee") announcing "Change role for {user}, currently…"; active = labeled text `Button` ("Activate"/"Deactivate {user}") replacing the glyph-only toggle; lock = display-only `FontIcon` shown only when locked plus "Locked"/"Not locked" text. No static per-row `AutomationId`s (they would duplicate).
- **Rationale**: Spec FR-003/FR-009 — actions must be clearly labeled, keyboard-operable, and announce which user they affect; Narrator cannot convey a bare checkmark glyph's meaning.
- **Alternatives considered**: `ToggleSwitch` for active (rejected — immediate-effect toggle on a security-sensitive flag with no confirm hook; explicit button keeps the existing confirm flow), keeping glyph buttons with tooltips (rejected — tooltips are mouse-only, invisible to keyboard/screen-reader users).

## R-04 — Forms as plain cards, not always-open Expanders

- **Decision**: `Border` cards replace the two always-open `Expander`s; `Description` hints, `UpdateSourceTrigger=PropertyChanged` bindings, 44px action buttons disabled while `IsBusy` kept/added.
- **Rationale**: Expanders that are always open add chrome and collapse machinery for zero benefit (spec FR-001 distinct sections); busy-gating implements FR-008 duplicate-submit prevention.
- **Alternatives considered**: Keep `Expander`s (rejected — visual noise, larger touch targets on headers that do nothing), `SettingsCard`/`SettingsExpander` (rejected — Windows Community Toolkit dependency for what plain cards already express; revisit if the app adopts the Toolkit elsewhere).

## R-05 — No new project, no new dependency, no HTTP change

- **Decision**: All work in `RAGGit.Client.Core` (ViewModel) + `RAGGit.Client.WinUI` (XAML, label converters). User-management endpoints, validation rules, and RBAC unchanged; lock stays display-only.
- **Rationale**: Constitution VII (simplicity); spec FR-012/Assumptions.
- **Alternatives considered**: None seriously — any server scope would contradict the spec.

## R-06 — Narrow-window reflow via VisualStateManager

- **Decision**: `VisualStateManager` + `AdaptiveTrigger` (`MinWindowWidth=0` state stacks the form cards via `Grid.ColumnSpan` setters; Wide declared first so first-match wins); list columns sized to fit ~640px without clipping.
- **Rationale**: Spec edge case ≤720px with no clipped unreachable controls, using the platform mechanism rather than code-behind resize handling.
- **Alternatives considered**: Code-behind `SizeChanged` reflow (rejected — layout belongs in XAML states), horizontal scrolling list (rejected — row actions must stay visible without panning).

## Reference grounding

- `winapp find-ui` had no admin scenario; its InfoBar guidance (persistent inline status) applied. In-repo authorities: `LoginPage`/`QueryPage` (`InfoBar`), `LibraryPage` (card/list/overlay empty state, `ProgressRing` overlay), 015 `UploadDialog` (`InfoBar` + `LiveSetting`, 44px buttons, `{ThemeResource}`-only brushes).
