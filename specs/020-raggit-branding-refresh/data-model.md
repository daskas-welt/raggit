# Data Model: Raggit Branding Refresh

**Feature**: `020-raggit-branding-refresh` | **Date**: 2026-09-22 | **Plan**: [plan.md](plan.md)

No persistence, API DTO, server entity, or client business state is added. The feature changes shell presentation resources and adds a static product-name surface.

## Product branding presentation state

| Field | Type | Meaning |
|-------|------|---------|
| `ProductName` | constant text | Exact visible product name: `Raggit`. |
| `BrandNameForeground` | theme brush | Accessible text/icon contrast for the product name in Light, Dark, and High Contrast modes. |
| `NavigationRailBackground` | theme brush | Background of the navigation pane for each theme. |
| `NavigationRailForeground` | theme brush | Normal navigation text and icon color. |
| `NavigationRailSelectedBackground` | theme brush | Selected navigation item background. |
| `NavigationRailSelectedForeground` | theme brush | Selected navigation item text and icon color. |
| `NavigationView*PaneBackground` | theme brushes | WinUI pane background variants for Left, compact/overlay, and Top modes. |

## Existing shell entities retained unchanged

### Navigation destination

Existing destination tag, label, icon, selected state, visibility, and Automation ID. Branding does not add a destination or change route behavior.

### User/session context

Existing role, identity type, username, display name, and dashboard profile card. Branding keeps this information separate from the product-name surface.

### Theme surface

The semantic shell and workspace resources that adapt when the system theme changes. High Contrast values map to system brushes and preserve focus/selection semantics.

## State behavior

```text
Shell created → Product name visible in pane header
Theme changed → Brand/rail/workspace resources update
Width <= 720 → NavigationView switches to Top mode; product name remains reachable
Width > 720 → NavigationView switches to Left mode; product name appears above destinations
Session/configuration error → Existing error surface remains visible; branding remains non-blocking
```

- Product identity is static and read-only.
- No new network request is required.
- No navigation destination or user permission is derived from branding state.
