# Implementation Plan: FontIcon Glyphs (Replace Converter-Driven Text Glyphs)

**Branch**: `012-fonticon-glyphs` | **Date**: 2026-09-17 | **Spec**: [spec.md](./spec.md)

**Input**: Locked decisions — empty unlocked cell · Button-with-icon (no ToggleSwitch) · lock icon-only · verified Segoe codepoints · `winui-design` skill governs implementation.

**Skill**: `winui-design` (loaded) — FontIcon default pick, `SymbolThemeFontFamily` fallback chain, IconElement-vs-IconSource slots, accessible-name rule. MS Learn reference: [Icons in Windows apps](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/icons) + [Segoe Fluent Icons font](https://learn.microsoft.com/en-us/windows/apps/design/style/segoe-fluent-icons-font) (codepoint table verified 2026-09-17).

## Summary

Replace the last text glyphs in the WinUI client (Admin Active ✓/✗, Locked 🔒/—) with `FontIcon` elements using verified Segoe codepoints. Converters keep bool→string contracts, emitting codepoint strings instead of text glyphs; usage sites swap text content for `FontIcon` with `Glyph` binding. Screen-reader names added per T008 standard. No behavior change.

## Technical Context

**Language/Version**: C# .NET 8 (`global.json` SDK `8.0.425`)

**Primary Dependencies**: `Microsoft.WindowsAppSDK 1.5.240311000` (unchanged). No new packages — `FontIcon` is inbox; `SymbolThemeFontFamily` resolves Segoe Fluent Icons (Win11) with automatic Segoe MDL2 Assets fallback (Win10 1809 floor).

**Storage**: N/A.

**Testing**: Existing `tests/unit` + `tests/contract` + `tests/integration` green with zero assertion changes (direct converter asserts BLOCKED by WinUI TFM boundary — XAML-compiler + grep + visual gates substitute). Manual: Admin list in Light/Dark/Contrast + screen-reader walkthrough.

**Target Platform**: Windows 10 1809 (17763) + Windows 11 — codepoints MUST exist in Segoe MDL2 Assets (E711/E72E/E73E verified in the shared PUA range).

**Project Type**: WinUI 3 desktop shell tweak (XAML + 2 WinUI converters).

**Performance Goals**: N/A (static icons, zero runtime cost).

**Constraints**: 1809 floor (MDL2-compatible codepoints only); Core/tests read-only; unlocked cell stays empty.

**Scale/Scope**: 2 converters in `src/RAGGit.Client.WinUI/Converters/ViewConverters.cs`, 2 usage sites in `src/RAGGit.Client.WinUI/Views/AdminUsersPage.xaml`, 0 test changes.

## Constitution Check

| Principle | Status | Evidence |
|-----------|--------|----------|
| **I. Single-Tenant On-Prem** | ✅ PASS | No tenant concept touched. |
| **II. Workstation-Owned AI** | ✅ PASS | UI-only; no models/vectors. |
| **III. .NET Library-First & Client Reuse** | ✅ PASS | Formatting logic stays in converters; XAML binds as before. |
| **IV. Offline Invariant** | ✅ PASS | No runtime egress; system font only. |
| **V. Citation-Grounded RAG** | ✅ PASS | Untouched. |
| **VI. Test-First** | ✅ PASS | Existing suites green as gate + XAML-compiler/grep/visual substitute gates (direct asserts blocked by TFM boundary — research.md Decision 4). |
| **VII. Simplicity & Proprietary Stewardship** | ✅ PASS | No new projects/packages (toolkit route already rejected in 011 research). |

*Gate: PASS, no amendment. Proceed.*

## Project Structure

```text
specs/012-fonticon-glyphs/
├── plan.md / spec.md / research.md / data-model.md / quickstart.md
├── contracts/icon-glyphs.md
└── checklists/requirements.md

src/RAGGit.Client.WinUI/
├── Converters/ViewConverters.cs   # WRITABLE this feature: ActiveGlyph/LockedGlyph emit codepoints
└── Views/AdminUsersPage.xaml      # WRITABLE: FontIcon usages + accessible names
src/RAGGit.Client.Core/           # READ-ONLY
tests/                            # READ-ONLY (no converter asserts possible across TFM boundary)
```

**Structure Decision**: Minimal two-file change. This feature explicitly lifts the 011 read-only rule for the two glyph converters only.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| None | No new projects, packages, or APIs. | n/a |

## Post-Design Constitution Re-check

| Principle | Status | Post-design evidence |
|-----------|--------|----------------------|
| I / II / IV / V / VII | ✅ PASS | Unchanged from pre-design check. |
| III | ✅ PASS | Converter contracts (bool→string) preserved; only emitted values change. |
| VI | ✅ PASS | research.md records codepoint verification; quickstart.md defines grep + visual validation (unit asserts blocked — Decision 4). |

*Design gate: PASS. Ready for tasks.*
