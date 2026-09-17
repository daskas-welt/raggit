# Implementation Plan: WinUI Redesign (Native Windows 11)

**Branch**: `011-ui-redesign` | **Date**: 2026-09-17 | **Spec**: [spec.md](./spec.md)

**Input**: Locked plan — full shell + all pages, native Win11, Dev Home anchor, reskin + violation fixes. Presentation-only; Core/API/contracts untouched.

## Summary

Convert the WinUI 3 client to native Windows 11 Fluent (Mica, system accent, Segoe UI Variable) anchored on Dev Home's nav/card language and PowerToys' `SettingsCard` patterns, fixing the winui-design audit violations (theme dictionaries, window sizing, icons, `InfoBar`, virtualization, binding triggers) in the same pass. No behavior change.

## Technical Context

**Language/Version**: C# .NET 8 (`global.json` SDK `8.0.425`)

**Primary Dependencies**: `Microsoft.WindowsAppSDK 1.5.240311000` (unchanged), `CommunityToolkit.Mvvm 8.2.2` via Core (unchanged). No new packages — all controls are inbox WinUI 3 + Windows App SDK.

**Storage**: None (unchanged; thin client).

**Testing**: Existing `tests/unit` + `tests/contract` + `tests/integration` must stay green with zero assertion changes (SC-003). Manual per-page checks: keyboard walkthrough, Contrast-theme pass, narrow-window reflow.

**Target Platform**: Windows 10 1809 (17763) + Windows 11 — note Mica falls back gracefully on Win10; High Contrast verified on Win11.

**Project Type**: WinUI 3 desktop shell reskin (`RAGGit.Client.WinUI` XAML-only, plus `MainWindow.xaml.cs` sizing).

**Performance Goals**: ≥20-message chat smooth; 100+ row lists virtualized; no `ScrollViewer`-wrapped collections.

**Constraints**: Offline invariant untouched (no runtime egress added); Core read-only (FR-007); `winapp find-ui` grounding recorded per surface before XAML edits (FR-008).

**Scale/Scope**: ~8 pages + 3 components + `App.xaml` + `MainWindow`; ~109 theme binding sites; 0 new projects.

## Constitution Check

| Principle | Status | Evidence |
|-----------|--------|----------|
| **I. Single-Tenant On-Prem** | ✅ PASS | No tenant concept touched. |
| **II. Workstation-Owned AI** | ✅ PASS | Still UI + HttpClient; no models/vectors added. |
| **III. .NET Library-First & Client Reuse** | ✅ PASS | Core reused unchanged; XAML-only changes. |
| **IV. Offline Invariant** | ✅ PASS | No runtime egress; existing WAN-disabled suite unchanged. |
| **V. Citation-Grounded RAG** | ✅ PASS | Citation rendering restyled only. |
| **VI. Test-First** | ✅ PASS | Existing suites green as gate; no new logic to TDD (presentation-only). |
| **VII. Simplicity & Proprietary Stewardship** | ✅ PASS | No new projects/packages; Dev Home reference-only. |

*Gate: PASS, no amendment. Proceed.*

## Project Structure

```text
specs/011-ui-redesign/
├── plan.md / spec.md / tasks.md / research.md / checklists/

src/RAGGit.Client.WinUI/          # ONLY change area (XAML + MainWindow sizing)
├── App.xaml                      # ThemeDictionaries Light/Dark/HighContrast
├── MainWindow.xaml(.cs)          # Nav icons, DPI-aware sizing, Upload ruling
├── Views/*.xaml(.cs)             # Per-page reskin (no VM/logic changes)
├── Components/*.xaml             # ChatControl, PaginationFooter, StatusChip
└── Converters/                   # Unchanged (read-only)
src/RAGGit.Client.Core/           # READ-ONLY
tests/                            # READ-ONLY, must stay green
```

**Structure Decision**: XAML + sizing code only; converters/ViewModels/Core/tests untouched. Reference repos (Dev Home, PowerToys, Gallery, Galileo, SmrtDoodle) are studied, never referenced as packages.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| None | No new projects, packages, or APIs. | n/a |

## Post-Design Constitution Re-check

| Principle | Status | Post-design evidence |
|-----------|--------|----------------------|
| I / II / IV / V / VII | ✅ PASS | Unchanged from pre-design check. |
| III | ✅ PASS | Tasks keep Core read-only; XAML binds existing VM properties only. |
| VI | ✅ PASS | tasks.md keeps full suites green as Phase 8 gate. |

*Design gate: PASS. Ready for tasks.*
