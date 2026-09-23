# Research: Dashboard Visual Refresh

**Feature**: `019-dashboard-visual-refresh` | **Date**: 2026-09-22 | **Plan**: [plan.md](plan.md)

## Decision: Keep the existing WinUI shell and use `NavigationView` as the anchor

**Rationale**: The current app already uses a left `NavigationView` with stable navigation Automation IDs. WinUI Gallery research (`gallery-navigationview-1`, `gallery-navigationview-2`) confirms `NavigationView` is the platform pattern for 2–7 top-level areas and supports left/compact/top display modes. Keeping it preserves keyboard semantics and minimizes route risk.

**Alternatives considered**:

- Custom navigation rail: rejected because it would duplicate selection, focus, accessibility, and responsive behavior already provided by WinUI.
- Replacing the shell with tabs: rejected because the app has five top-level destinations and an admin-only destination, which fit `NavigationView` better than `TabView`.

## Decision: Add a dashboard landing page, but do not add a new server endpoint

**Rationale**: `LibraryViewModel`, `HistoryViewModel`, and `DocumentsMineViewModel` already expose counts and recent items. A `DashboardViewModel` in `RAGGit.Client.Core` can compose these existing clients and expose transient summary state. The dashboard will load only existing data, show metrics that are actually available, and omit unsupported aggregates. It must avoid issuing duplicate equivalent loads where the existing page state can be reused.

**Alternatives considered**:

- Make Library the dashboard by adding summary panels above its list: rejected because it keeps the current landing page coupled to the document table and makes the reference-style overview hard to scan.
- Add a `/dashboard` API endpoint: rejected by FR-011 and the presentation-only scope; it would add contract and offline regression surface without user value.
- Calculate whole-history trends and averages: rejected because the current history API is paged and does not guarantee complete aggregate data.

## Decision: Use semantic purpose-based resources for the blue rail and metric surfaces

**Rationale**: Existing `App.xaml` has Light, Dark, and High Contrast dictionaries. Add purpose-based resources such as `NavigationRailBackground`, `NavigationRailForeground`, `NavigationRailSelectedBackground`, `NavigationRailSelectedForeground`, `DashboardSurface`, `MetricTileBackground`, and `MetricTileBorder`. Use `{ThemeResource}` at consumption sites. High Contrast maps to system brushes rather than trying to preserve decorative blue.

**Alternatives considered**:

- Hard-code the reference image's blue in XAML: rejected because it fails theme switching and High Contrast requirements.
- Replace all existing semantic resources: rejected because it would widen the visual regression scope; new resources can layer on the established baseline.

## Decision: Treat the reference image as hierarchy and palette direction, not domain content

**Rationale**: The image communicates a left blue rail, compact top metrics, a personal greeting, and two-column content panels. RAGGit should translate those patterns into document/query activity rather than show banking terms, unsupported financial metrics, or copied assets. This keeps content truthful and aligned with citation-grounded product behavior.

**Alternatives considered**:

- Pixel-copy the image: rejected because its content model is unrelated to RAGGit and would imply unsupported data.
- Use generic SaaS cards everywhere: rejected because it loses the image's strong navigation rail and compact dashboard hierarchy and conflicts with the existing Windows-native baseline.

## Decision: Use `NavigationView` compact/top behavior below the 720px breakpoint

**Rationale**: Existing Admin work uses `AdaptiveTrigger` at 720px, and the specification requires no clipped controls at that width. A responsive shell should switch to compact or top navigation, while dashboard columns stack. Existing collection controls keep ownership of scrolling to preserve virtualization.

**Alternatives considered**:

- Keep a fixed left rail at every width: rejected because it consumes too much space and conflicts with the narrow-window requirement.
- Wrap the whole page in one `ScrollViewer`: rejected because it can disable or degrade `ListView` virtualization and creates nested-scroll traps.

## Decision: Preserve existing Automation IDs and add IDs only for new dashboard interactions

**Rationale**: The project has stable IDs such as `NavLibrary`, `NavAsk`, `RefreshButton`, `ChatInputBox`, and `UploadButton`. Existing UI validation is primarily XAML compilation plus manual keyboard/Narrator/theme checks, so stable IDs are the low-cost compatibility seam for future UI automation.

**Alternatives considered**:

- Rename IDs to match the new visual labels: rejected because existing checks and user workflows depend on them.
- Add a new UI automation framework: rejected as out of scope; the repository has no dedicated UI test project and the established presentation precedent uses manual WinUI validation.

## Design grounding sources

- WinApp CLI: `gallery-navigationview-1`, `gallery-navigationview-2` for navigation control selection.
- `specs/011-ui-redesign/research.md` for native WinUI, semantic themes, Mica, and virtualization baseline.
- `specs/012-fonticon-glyphs/research.md` for icon and accessible-name conventions.
- `specs/015-file-upload-ui/research.md` for `InfoBar`, live status, and shared ViewModel presentation state.
- `specs/016-admin-redesign/research.md` for adaptive layout, list-owned scrolling, and admin visibility.
- Supplied reference image for blue rail, welcoming header, compact metric row, and chart/content panel hierarchy.
