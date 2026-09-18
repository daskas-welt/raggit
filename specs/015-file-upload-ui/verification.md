# Walkthrough Record: File Upload UI (T028)

**Date**: 2026-09-18 | **Method**: automated/static audit (no interactive desktop session available)

## Verified in code (pass)

- Queue `ListView` virtualized (`MaxHeight=260`), never in a `ScrollViewer`; per-row `ProgressBar` + inline error; overall `CompletedCountText` ("2 of 4 done").
- Summary + rejection `InfoBar`s, both `LiveSetting="Polite"`; `SymbolIcon` Add/Upload/Cancel/Delete; `{ThemeResource}`-only brushes (0 `#RRGGBB` literals).
- 44px targets on all actions; `AutomationId` on every interactive control; focus into Add-files on open; `Closing` cancels in-flight upload; `IsAllSucceeded` → ~900ms success → auto-close; failures stay open; focus returns to Library Upload button (`LibraryPage.xaml.cs`).
- Library header Upload is the sole entry, admin-gated (`IsAdmin` binding + code guard); no Upload nav item.
- Build 0/0; upload tests 18/18 green.

## Attempted live

- Tried to boot the workstation API for a live UI run: crashes at startup with `lancedb.LanceDbException: Table 'library' was not found` (pre-existing environment issue — same root as the 2 failing unit tests). A scratch-DB boot also stalled. No live run, no screenshots possible until the workstation data issue is resolved.

## Still needs a human (5 min)

- Narrator audio pass (announcements), visual eyeball in Light/Dark/HighContrast + ≤720px, one live multi-file upload + cancel against a healthy workstation.
