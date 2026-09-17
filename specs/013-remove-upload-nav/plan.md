# Implementation Plan: Remove Upload Nav Item

**Branch**: `013-remove-upload-nav` | **Date**: 2026-09-18 | **Spec**: [spec.md](./spec.md)

**Input**: Single-entry-point restoration — Upload lives only in the Document Library header; nav item removed. Reverses the 011 T003 ruling, restores the 008 rule.

## Summary

Delete the Upload `NavigationViewItem`, its `"upload"` selection branch (including the dialog-open helper if left unreferenced), and its visibility line from `MainWindow`, leaving the Library header button flow byte-identical. No behavior change beyond the nav surface.

## Technical Context

**Language/Version**: C# .NET 8 (`global.json` SDK `8.0.425`)

**Primary Dependencies**: `Microsoft.WindowsAppSDK 1.5.240311000` (unchanged). No new packages.

**Storage**: N/A.

**Testing**: Existing `tests/unit` + `tests/contract` + `tests/integration` green with zero assertion changes. Manual: nav pane shows 5 items per role; Library upload end-to-end.

**Target Platform**: Windows 10 1809 (17763) + Windows 11 (unchanged).

**Project Type**: WinUI 3 desktop shell tweak (`MainWindow.xaml` + `MainWindow.xaml.cs` only).

**Performance Goals**: N/A (removal).

**Constraints**: Library header Upload flow untouched (button, dialog, refresh, admin gating); Core/tests read-only; pane selection must keep reflecting the current page.

**Scale/Scope**: 2 files (`MainWindow.xaml`, `MainWindow.xaml.cs`); net-negative lines.

## Constitution Check

| Principle | Status | Evidence |
|-----------|--------|----------|
| **I. Single-Tenant On-Prem** | ✅ PASS | Untouched. |
| **II. Workstation-Owned AI** | ✅ PASS | Untouched. |
| **III. .NET Library-First & Client Reuse** | ✅ PASS | No logic moves; dialog stays Library-owned. |
| **IV. Offline Invariant** | ✅ PASS | No runtime egress change. |
| **V. Citation-Grounded RAG** | ✅ PASS | Untouched. |
| **VI. Test-First** | ✅ PASS | Existing suites green as gate; removal verified by build + nav walkthrough (no new logic to TDD). |
| **VII. Simplicity & Proprietary Stewardship** | ✅ PASS | Net-negative code; single entry point restored. |

*Gate: PASS, no amendment. Proceed.*

## Project Structure

```text
specs/013-remove-upload-nav/
├── plan.md / spec.md / research.md / data-model.md / quickstart.md
├── contracts/nav-items.md
└── checklists/requirements.md

src/RAGGit.Client.WinUI/
├── MainWindow.xaml        # WRITABLE: remove UploadItem
└── MainWindow.xaml.cs     # WRITABLE: remove upload branch + visibility line (+ helper if unreferenced)
src/RAGGit.Client.Core/   # READ-ONLY
tests/                    # READ-ONLY
```

**Structure Decision**: Two-file removal. Upload dialog, Library button, and ViewModels untouched.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| None | Removal only. | n/a |

## Post-Design Constitution Re-check

| Principle | Status | Post-design evidence |
|-----------|--------|----------------------|
| I / II / IV / V / VII | ✅ PASS | Unchanged from pre-design check. |
| III | ✅ PASS | No logic relocation; Library owns upload as before. |
| VI | ✅ PASS | research.md records the removal inventory; quickstart.md defines build + nav validation. |

*Design gate: PASS. Ready for tasks.*
