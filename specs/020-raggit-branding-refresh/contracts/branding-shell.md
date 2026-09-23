# Contract: Raggit Branding Shell

**Feature**: `020-raggit-branding-refresh` | **Date**: 2026-09-22 | **Plan**: [plan.md](../plan.md)

No HTTP or server contract changes. This contract documents the changed client shell surface.

## Navigation shell

```text
MainWindow
└── NavigationView
    ├── PaneHeader: non-interactive product name "Raggit"
    ├── Dashboard   (NavDashboard, existing)
    ├── Library     (NavLibrary, existing)
    ├── Ask         (NavAsk, existing)
    ├── History     (NavHistory, existing)
    ├── My Docs     (NavMyDocs, existing)
    └── Admin       (NavAdmin, existing, admin-only)
```

## Branding rules

- Product name text MUST be exactly `Raggit`.
- Product name MUST be non-interactive and MUST NOT change navigation selection.
- Product name MUST have a meaningful accessible name, such as `Product name Raggit`.
- Existing navigation IDs, tags, labels, icons, role gating, and routes MUST remain unchanged.
- The shell background MUST use the semantic navigation rail resource rather than the workspace surface resource.
- Pane background variants MUST cover Left, compact/overlay, Top, and High Contrast modes.

## Responsive rules

- Above 720px: left navigation mode with `Raggit` above destinations.
- At or below 720px: existing Top navigation mode remains active; `Raggit` must remain visible or reachable without clipping.
- Dashboard header, metrics, and content panels continue using the existing responsive stacking behavior.

## Accessibility and theme rules

- Light, Dark, and High Contrast dictionaries provide semantic branding values.
- High Contrast uses system brushes; decorative blue is not required in that mode.
- Product name and selected/focus states remain understandable without color alone.
- Existing interactive Automation IDs remain stable.
