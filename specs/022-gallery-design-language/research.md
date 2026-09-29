# Research: Gallery-Style Design Language

**Feature**: `022-gallery-design-language` | **Date**: 2026-09-29 | **Plan**: [plan.md](plan.md)

Records the decisions that resolve the plan's Technical Context. All findings were verified against
the installed `Wpf.Ui.dll` 4.3.0 (public types, base types, and enum members via reflection;
resource-key strings from the assembly) and the WPF-UI Gallery source
(`github.com/lepoco/wpfui`, `main`), not from memory. The Gallery tracks `main` and may be newer than
4.3.0, so every control/property used here was confirmed present in 4.3.0.

## Decision: Complete the existing library adoption; borrow the Gallery's patterns, not its code

**Rationale**: The client already references WPF-UI 4.3.0 and uses `ui:FluentWindow` +
`ui:NavigationView`. The Gallery is a reference for *patterns* (hero banner, tile cards, settings
rows, searchable grouped navigation), not for copy-paste — its `main` branch may use newer members.
Every control and property named in this plan was verified present in 4.3.0.

**Alternatives considered**:
- Copy the Gallery's XAML directly: rejected — version skew (`main` vs 4.3.0) and it carries
  Gallery-specific controls (`PageControlDocumentation`), assets, and i18n.

## Decision: Shell = Mica backdrop, centered, pane toggle, transition, grouped/searchable nav

**Rationale**: `FluentWindow.WindowBackdropType` / `WindowCornerPreference` / `WindowStartupLocation`
and `NavigationView.IsPaneToggleVisible` / `OpenPaneLength` / `Transition` / `IsTopSeparatorVisible` /
`IsFooterSeparatorVisible` / `FrameMargin` / `AutoSuggestBox` / `Header` are all present in 4.3.0.
This is the lowest-risk, highest-visibility change and matches the Gallery's window verbatim.

**Alternatives considered**:
- Leave chrome as-is: rejected — the brief is explicitly Gallery parity.
- Use the Gallery's `BreadcrumbBar` header: deferred — the client's navigation is flat; a breadcrumb
  adds little and is out of the agreed scope.

## Decision: Navigation grouping and search live in the WPF project's `MainWindowViewModel`

**Rationale**: `MainWindowViewModel` is in `src/RAGGit.Client.WPF/ViewModels` (the WPF project), not
in `RAGGit.Client.Core`. Group headings are modelled by inserting
`ui:NavigationViewItemHeader` entries into the existing `MenuItems` collection; search filters that
collection on `AutoSuggestBox.TextChanged`. This keeps the change presentation-only (FR-014).

**Alternatives considered**:
- Move grouping into `RAGGit.Client.Core`: rejected — violates the behavior-freeze constraint.

## Decision: Hero banner uses a theme-accent gradient built from library **Color** tokens

**Rationale**: `GradientStop.Color` needs a `Color`, not a `Brush`. 4.3.0 ships the Color tokens
`SystemAccentColor`, `SystemAccentColorPrimary`, `SystemAccentColorSecondary`,
`SystemAccentColorTertiary` (plus `…Brush` variants) and `TextOnAccentFillColorPrimary` (Color +
Brush). A `LinearGradientBrush` from `SystemAccentColorPrimary` → `SystemAccentColorSecondary` with
`TextOnAccentFillColorPrimaryBrush` text is fully theme-adaptive (Light/Dark/High Contrast) and
follows the user's Windows accent, with **zero literals** (FR-010, FR-011). A low-opacity
`ui:SymbolIcon` watermark adds depth without imagery (offline-safe).

**Alternatives considered**:
- The Gallery's photo + white text: rejected — white literals violate FR-010 and the asset breaks
  offline-first/theme adaptivity.
- A solid `AccentFillColorDefaultBrush` hero: acceptable fallback, but the gradient is the requested
  look and is equally token-safe.

## Decision: Dashboard tiles are `ui:CardAction` driven by code-behind `Click`

**Rationale**: `CardAction` derives from `System.Windows.Controls.Primitives.ButtonBase`, so it
exposes `Click` as well as `Command`. Using `Click` handlers keeps the existing
`OnLibraryClicked` / `OnAskClicked` code-behind navigation and avoids adding commands to
`RAGGit.Client.Core` (FR-014).

**Alternatives considered**:
- Add `RelayCommand`s to `DashboardViewModel` (Core): rejected — touches the shared behavior layer.
- Fake the look with a styled `ui:Button`: rejected — `CardAction` is the library's intended control
  and provides hover/selection/chevron for free.

## Decision: Settings becomes `ui:CardControl` rows under section headings

**Rationale**: `CardControl` exposes `Icon` (IconElement), `Header` (object), and `Content`, which is
exactly the Gallery's settings-row pattern (verified in the Gallery's `SettingsPage.xaml`). The
existing theme radios, value texts, and sign-out button move inside rows unchanged, so their
automation IDs survive (FR-009).

**Alternatives considered**:
- Keep stacked `ui:Card`s: rejected — the row pattern is the requested improvement.
- Use `CardExpander` for everything: rejected — only verbose sections warrant collapsing; rows are
  clearer for single settings.

## Decision: Responsive tile layout via `WrapPanel`/`UniformGrid`, not a fixed 3-column `Grid`

**Rationale**: The Gallery uses a fixed 3-column `Grid`, which clips at the minimum window size
(800×600). A wrapping panel reflows tiles to fewer columns, satisfying FR-013/SC-005.

**Alternatives considered**:
- Fixed 3 columns like the Gallery: rejected — fails the minimum-size requirement.

## Decision: All icon names are validated `SymbolRegular` members

**Rationale**: XAML enum values resolve at **runtime**, so an invalid icon name compiles but throws
`XamlParseException` when the view loads (this exact class of bug was found and fixed on the shell in
feature 021: `ChevronDoubleLeft24`, `Copy`, `Delete`, `Document`, `ArrowUpload` were all invalid).
Every icon used here was checked against the enum. Verified valid: `Home24`, `Library24`, `Chat24`,
`History24`, `Document24`, `People24`, `Settings24`, `Search24`, `Color24`, `Link24`, `Person24`,
`PlugConnected24`, `CloudArrowUp24`, `SignOut24`, `BookInformation24`, `Info24`, `ArrowClockwise24`,
`Folder24`, `Dismiss24`. (`ShieldPerson24` does **not** exist — avoided.)

**Alternatives considered**:
- Assume names from the Fluent icon catalogue: rejected — the runtime-only failure mode is silent
  until the page loads.

## Decision: Preserve the frozen automation-ID contract; add IDs for new interactive elements

**Rationale**: Feature 021 froze 52 literal IDs plus 3 bound IDs; UI-automation consumers depend on
them. New interactive elements (nav search box, Dashboard tiles, Settings rows) get new stable IDs.
Existing IDs (`DashboardMetricDocuments`, `DashboardMetricQueries`, `DashboardProfileCard`,
`DashboardAskButton`, `DashboardLibraryButton`, `DashboardRefreshButton`, `DashboardStatusBar`,
`DashboardRetryButton`, `Nav*`, `LightThemeRadio`, `DarkThemeRadio`, `SignOutButton`,
`WorkstationUrlValue`, `SignedInAsValue`, `ConnectionStatusValue`) MUST survive the re-skin.

**Alternatives considered**:
- Rename IDs to match new controls: rejected — breaks existing validation and automation.

## Decision: Repeat feature 021's static contract checks and add an icon-name check

**Rationale**: 021 established C2 (no color literals), C3 (no literal glyphs), C4 (ID superset),
C6 (no `ScrollViewer`-wrapped virtualizing list). This feature reuses them and adds a
`SymbolRegular`-name audit to catch the runtime-only failure mode before the UI walkthrough.

**Alternatives considered**:
- Rely on the build: rejected — icon-name validity is not a compile-time check.

## Design grounding sources

- Installed `Wpf.Ui.dll` 4.3.0: control types, `CardAction` base type (`ButtonBase`), `CardControl`/
  `AutoSuggestBox`/`NavigationView` members, `ControlAppearance`/`FontTypography`/`SymbolRegular`
  members, and accent/on-accent resource-key strings.
- WPF-UI Gallery `main`: `Views/Windows/MainWindow.xaml` (shell), `Views/Pages/DashboardPage.xaml`
  (hero + `CardAction` tiles + `HyperlinkButton` links), `Views/Pages/SettingsPage.xaml`
  (`CardControl` rows, `CardExpander`, `Anchor`).
- In-repo baselines: `specs/021-wpfui-modernization` (frozen ID set, theme-resource stance, contract
  checks), `docs/architecture.md`.
- `src/RAGGit.Client.WPF` audit: current `MainWindow.xaml`, `DashboardPage.xaml`, `SettingsPage.xaml`,
  and `MainWindowViewModel.cs`.
