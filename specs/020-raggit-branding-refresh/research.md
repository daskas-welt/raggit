# Research: Raggit Branding Refresh

**Feature**: `020-raggit-branding-refresh` | **Date**: 2026-09-22 | **Plan**: [plan.md](plan.md)

## Decision: Use the existing NavigationView pane header for `Raggit`

**Rationale**: The shell already uses WinUI `NavigationView`, and WinUI supports free-form pane-header content above the navigation destinations. A non-interactive `TextBlock` keeps product identity visible without adding a fake route, changing selection behavior, or disturbing existing Automation IDs.

**Alternatives considered**:

- Add `Raggit` as a navigation item: rejected because product identity is not a destination and would confuse keyboard navigation and selection.
- Add a custom outer rail: rejected because it would duplicate `NavigationView` layout, focus, responsive behavior, and accessibility.
- Add a logo image asset: rejected because the request asks for the app name and the spec explicitly keeps the first pass text-only.

## Decision: Correct the shell background to use the semantic rail resource

**Rationale**: The current `NavigationView` binds its background to `DashboardSurface`, while the semantic rail resources already exist. Binding the shell to `NavigationRailBackground` makes the left region visibly blue in Light mode and adapts it independently in Dark and High Contrast modes.

**Alternatives considered**:

- Add a literal blue `Color` to `MainWindow.xaml`: rejected because it would bypass theme switching and High Contrast.
- Use `DashboardSurface` for the whole shell: rejected because it erases the rail/workspace separation shown in the reference.

## Decision: Extend the existing Light/Dark/High Contrast dictionaries

**Rationale**: `App.xaml` already defines semantic rail, selected-state, dashboard, metric, and system-brush resources. Branding should refine those keys and add only `BrandNameForeground` if needed. High Contrast should continue to use Windows system brushes instead of forcing reference blue.

**Alternatives considered**:

- Introduce a second branding resource dictionary: rejected because it creates competing theme sources and increases maintenance risk.
- Copy the Light palette into Dark mode: rejected because the blue/white contrast relationship would become unreadable or visually harsh.

## Decision: Preserve the existing 720px responsive behavior

**Rationale**: `MainWindow.xaml.cs` already changes `NavigationViewPaneDisplayMode` to `Top` at 720px, and `DashboardPage.xaml.cs` already stacks header, metrics, and content panels at that width. The branding task only needs to verify the pane header remains visible and does not consume the route surface.

**Alternatives considered**:

- Add a second mobile navigation implementation: rejected because the existing top-mode `NavigationView` already provides the platform behavior.
- Keep a fixed desktop rail at narrow widths: rejected because it conflicts with existing responsive requirements.

## Decision: Preserve all existing interaction identifiers and route tags

**Rationale**: Existing IDs (`NavDashboard`, `NavLibrary`, `NavAsk`, `NavHistory`, `NavMyDocs`, `NavAdmin`) and dashboard/profile IDs are established compatibility seams. Branding changes must not rename, reorder, or replace them.

**Alternatives considered**:

- Rename IDs to match brand terminology: rejected because it would break UI validation and existing automation consumers.

## Design grounding sources

- Supplied reference image: blue left rail, light workspace, branded top-left identity hierarchy.
- WinUI NavigationView pane background resources: `NavigationViewExpandedPaneBackground`, `NavigationViewDefaultPaneBackground`, and `NavigationViewTopPaneBackground`.
- `specs/011-ui-redesign/research.md`: native WinUI, semantic themes, responsive shell, and accessibility baseline.
- `specs/019-dashboard-visual-refresh/research.md`: current dashboard resources, profile card, stable IDs, and 720px behavior.
- `winui-design` guidance: purpose-based resources, system brushes for High Contrast, and no custom navigation when NavigationView fits.
