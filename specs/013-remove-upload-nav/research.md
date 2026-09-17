# Research: Remove Upload Nav Item (013)

**Date**: 2026-09-18 · No unknowns — removal restores the 008 rule; decisions recorded for traceability.

## Decision 1 — Removal inventory (from T007 state)

- Decision: remove exactly three things — (1) the `UploadItem` `NavigationViewItem` in `MainWindow.xaml`, (2) the `"upload"` case branch in `Nav_SelectionChanged` (including `OpenUploadDialogAsync` + `NavItemForPage` only if left unreferenced — `NavItemForPage` maps page types, none of which is upload, so it stays), (3) the `UploadItem.Visibility` line in `RefreshAdminVisibility`.
- Rationale: restores the 008 single-entry-point rule with the smallest diff; Library header button, dialog, refresh, and gating stay byte-identical.
- Alternatives considered: hiding the item instead of deleting — rejected, dead XAML + dead branch is exactly the drift 010 cutover was meant to kill.

## Decision 2 — Selection state after removal

- Decision: no selection-restore logic needed — with no Upload item, selection can never point at it; the remaining cases navigate normally.
- Rationale: the T007 restore helper existed only to serve the Upload item; plain navigation needs no compensation.
- Alternatives considered: keep `OpenUploadDialogAsync` for reuse — rejected, Library page already owns an identical flow; dead code goes.

## Decision 3 — Tests

- Decision: existing suites green as the gate; manual nav walkthrough (5 items per role, Library upload end-to-end) as validation. No new tests — removal, not logic.
- Rationale: Constitution VI intent preserved at proportionate cost.
