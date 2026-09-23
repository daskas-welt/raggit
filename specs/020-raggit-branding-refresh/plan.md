# Implementation Plan: Raggit Branding Refresh

**Branch**: `020-raggit-branding-refresh` | **Date**: 2026-09-22 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/020-raggit-branding-refresh/spec.md`

## Summary

Add the exact `Raggit` product name to the existing WinUI navigation pane header and align the shell palette with the supplied reference image. Use the existing semantic theme dictionaries for a vivid blue Light rail, adapted Dark values, and system-aware High Contrast values. Correct the shell background to use the rail resource, preserve the current responsive top-navigation behavior at 720px, and leave all routes, permissions, Automation IDs, dashboard/profile behavior, APIs, and RAG workflows unchanged.

## Technical Context

**Language/Version**: C# / XAML, .NET 10 (`net10.0-windows10.0.17763.0` for the WinUI client)

**Primary Dependencies**: Windows App SDK 2.4.0 (WinUI 3), existing `NavigationView`, existing semantic `ThemeDictionaries`, and current `RAGGit.Client.WinUI` shell resources

**Storage**: N/A; branding is transient presentation state only

**Testing**: WinUI XAML compilation, xUnit unit/contract/integration/offline regression suites, CSharpier, and manual keyboard/Narrator/theme/responsive checks

**Target Platform**: Windows 10 1809+ / Windows 11 desktop, single-project MSIX

**Project Type**: WinUI 3 desktop client shell

**Performance Goals**: No measurable runtime cost beyond the existing shell; product name and navigation render without layout clipping at desktop and 720px-or-narrower widths

**Constraints**: Presentation-only; semantic Light/Dark/High Contrast resources; no hard-coded feature colors in usage sites; existing routes, permissions, Automation IDs, list virtualization, profile card, citations, upload flow, and offline behavior unchanged

**Scale/Scope**: Main shell and application theme dictionaries; one product-name pane header; six existing top-level navigation destinations; desktop and <=720px responsive modes

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Single-Tenant On-Prem**: PASS — no tenant or data behavior changes.
- **II. Workstation-Owned AI**: PASS — no client AI, model, vector, or network behavior changes.
- **III. .NET Library-First & Client Reuse**: PASS — existing WinUI shell and resources are reused; no new project or library is needed.
- **IV. Offline Invariant**: PASS — no request path, WAN dependency, or workstation behavior changes.
- **V. Citation-Grounded RAG**: PASS — answer/citation surfaces are untouched.
- **VI. Test-First**: PASS — XAML compilation and existing regression suites remain gates; no new data behavior requires a new library test.
- **VII. Simplicity & Proprietary Stewardship**: PASS — one shell XAML change and semantic resource updates; no dependencies, projects, storage, or packaging changes.

**Pre-Phase-0 gate**: PASS. No constitutional violation requires complexity tracking.

## Project Structure

### Documentation (this feature)

```text
specs/020-raggit-branding-refresh/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
└── tasks.md                 # created by /speckit.tasks
```

### Source Code (repository root)

```text
src/
└── RAGGit.Client.WinUI/
    ├── App.xaml              # semantic branding and NavigationView resources
    └── MainWindow.xaml       # Raggit pane-header branding and shell binding

tests/
└── existing unit, contract, integration, and UI/build validation suites
```

**Structure Decision**: Preserve the existing single-solution layout. Change only `App.xaml` and `MainWindow.xaml` unless compilation or accessibility validation requires a minimal shell code-behind adjustment. No server/API contract or data-model files are involved.

## Complexity Tracking

No constitutional violations require additional complexity justification.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — (none) | — | — |

**Post-Phase-1 gate**: PASS — the design adds only semantic shell resources and a non-interactive product-name surface.
