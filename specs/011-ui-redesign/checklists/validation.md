# Validation Record: WinUI Redesign (T020)

**Feature**: `011-ui-redesign` · **Date**: 2026-09-17
**Scope**: automated gates (agent-verified) + manual passes (defined here, to be run on hardware).

## Automated gates — all green [x]

- [x] `dotnet build RAGGit.sln` — 0 errors (11 pre-existing warnings only)
- [x] `tests/unit` 228/228 · `tests/contract` 78/78 · `tests/integration` 62/62 isolated (one load-dependent `Ingest_50PagePdf` perf flake under full-suite parallel load; passes standalone — pre-existing, unrelated to XAML)
- [x] `dotnet csharpier check .` — 215 files clean (7 XAML files reformatted post-edit, rebuild verified)
- [x] Zero `{StaticResource}` theme-key sites remain (109 converted); zero hard-coded color literals in WinUI XAML
- [x] Core / converters / tests untouched (verified via `git status` scope per commit)

## Per-page record

| Page | Automated evidence | Manual pass (TODO) | find-ui conformance |
|---|---|---|---|
| Shell (`MainWindow`) | Builds; `SelectionChanged` nav; icons compile | Keyboard traverse nav; Contrast pane; ≤720px Top-pane behavior | gallery-navigationview-3 (responsive), -1 |
| Library | Builds; bindings/handlers identical; in-ListView h-scroll | 0/1/50+ docs × 3 themes; row-action keyboard reach | Border (gallery-border-1) card idiom |
| Ask + ChatControl | Builds; Row-2 collision eliminated structurally; stretch verified in markup | 20-msg order × 3 themes; citations readable | No chat sample exists — bespoke (recorded) |
| History / My Docs | Builds; dead `StatusMessage` bindings removed (VM-confirmed) | 100-row scroll smoothness; empty states × 3 themes | Same card idiom |
| QueryDetail | Builds; `ScrollViewer` wrapper removed, list in `*` row | Citations scroll; Back focus return | Same card idiom |
| Admin | Builds; confirms fire via Click handlers | Screen-reader form walkthrough; keyboard-only user create/reset | toolkit-settingscard-4 pattern adapted to inbox Expander (22621 floor blocks toolkit — see research.md) |
| Login | Builds; per-keystroke binds compile; InfoBar bool-bound | Error surfaces while typing × 3 themes | n/a |

## Manual pass procedure (run on Win11 + Contrast themes)

1. Launch unpackaged Debug; walk every nav destination by keyboard only.
2. Switch Light → Dark → Contrast; screenshot each page (feeds docs refresh).
3. Resize to 720px narrow; confirm h-scroll (Library) / Top pane (shell), no clipped-orphan content.
4. Force 401 mid-browse → sign-in return; delete/role/reset confirms show identity + Cancel.
