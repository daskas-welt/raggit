# Data Model: Gallery-Style Design Language

**Feature**: `022-gallery-design-language` | **Date**: 2026-09-29 | **Plan**: [plan.md](plan.md)

This feature introduces no persisted data and no API contract. Its "model" is the audit the plan
applies: the surfaces it changes, the library tokens it binds to, the navigation/tile/row shapes, and
the frozen automation-ID contract. (The client-wide design system is owned by feature `023`.)

Baselines below are the measured pre-change state of `src/RAGGit.Client.WPF`.

## Entity: Surface

One row per changed surface. Drives FR-001…FR-008.

| Surface | File(s) | Pre-change | Post-change |
|---------|---------|-----------|-------------|
| Shell (window + nav) | `Views/MainWindow.xaml(.cs)`, `ViewModels/MainWindowViewModel.cs` | plain `FluentWindow`; `NavigationView` with flat `MenuItemsSource`; no backdrop/toggle/search/transition/grouping | Mica backdrop, centered; pane toggle + width + transition, no top/footer separators; `AutoSuggestBox` search; grouped sections + footer Settings |
| Dashboard | `Views/Pages/DashboardPage.xaml(.cs)` | plain "Welcome back" header + profile card; 2 button-shaped metric tiles + 2 stat cards; 2 content cards | accent-gradient hero (welcome/role/tagline); `ui:CardAction` destination tiles (icon + title + description); content cards retained |
| Settings | `Views/Pages/SettingsPage.xaml(.cs)` | 3 × `ui:Card` with bare `RadioButton`s / value texts / sign-out button | `ui:CardControl` rows (icon + title + description + control/value) under `BodyStrong` section headings |

## Entity: Theme Token

Replaces literals and defines the hero. Drives FR-010, FR-011, SC-003, SC-006.

| Token | Kind | Usage |
|-------|------|-------|
| `SystemAccentColorPrimary` → `SystemAccentColorSecondary` | Color (gradient stops) | Hero banner `LinearGradientBrush` |
| `TextOnAccentFillColorPrimaryBrush` | Brush | Hero title/subtitle/tagline text |
| `AccentFillColorDefaultBrush` | Brush | Tile/row accent icon foreground; primary actions |
| `TextFillColorSecondaryBrush` (or `Appearance="Secondary"`) | Brush | Secondary text in rows, tiles, subtitles |
| `CardBackgroundFillColorDefaultBrush` / `ControlFillColorDefaultBrush` | Brush | Tile/row surfaces |
| `SystemFillColorCriticalBrush` / `…SuccessBrush` / `…NeutralBrush` | Brush | Status chip / connection tones (reused) |

**Validation rules**: zero `#[0-9A-Fa-f]{6,8}` and zero `Color.FromRgb`/`Colors.` in
`RAGGit.Client.WPF` after the change; the hero gradient uses only `SystemAccentColor*` stops.

## Entity: Navigation Model

Drives FR-002…FR-005.

| Field | Description |
|-------|-------------|
| `Items` | Dashboard, Library, Ask, History, My Documents, then Admin (present only for administrators) — a single ordered group, no section headings |
| `FooterItems` | Settings (pinned) |

**Validation rules**: the full ordered list is always shown (no filtering — the search field was
removed during implementation review); Settings is always in the footer; the Admin item appears iff
the user is an administrator; the pane toggle collapses/expands without clipping content.

## Entity: Dashboard Tile

Drives FR-007, SC-002.

| Field | Description |
|-------|-------------|
| `Icon` | Valid `SymbolRegular` member (see Icon Contract) |
| `Title` | Short destination name |
| `Description` | One-line supporting text |
| `Target` | Navigation destination |
| `AutomationId` | Stable ID (existing ID preserved where a tile replaces an existing control) |

Suggested tiles: Library (`Library24`), Ask (`Chat24`), History (`History24`), My Documents
(`Document24`); Admin (`People24`) for administrators. Numeric metrics (documents, questions, ready,
mine) become a compact stat line inside/below the hero.

**Validation rules**: every tile has icon + title + description and navigates on a single
activation; tiles reflow (wrap) below the minimum width; no tile clips at 800×600.

## Entity: Settings Row

Drives FR-008.

| Field | Description |
|-------|-------------|
| `Icon` | Valid `SymbolRegular` member |
| `Title` | Setting name |
| `Description` | Supporting text (what the setting does) |
| `Control` | The control or read-only value (radio pair, value text, button) |
| `AutomationId` | Existing ID preserved |

Rows: App theme (`Color24`, keeps `LightThemeRadio`/`DarkThemeRadio`), Workstation URL (`Link24`,
`WorkstationUrlValue`), Signed in as (`Person24`, `SignedInAsValue`), Connection status
(`PlugConnected24`, `ConnectionStatusValue`), Sign out (`SignOut24`, `SignOutButton`).

**Validation rules**: each row renders icon + title + description + control/value; every control
remains keyboard reachable with visible focus; existing IDs unchanged.

## Entity: Icon Contract

Drives FR-012.

| Use | Verified-valid `SymbolRegular` |
|-----|-------------------------------|
| Nav | `Home24`, `Library24`, `Chat24`, `History24`, `Document24`, `People24`, `Settings24`, `Search24` |
| Settings rows | `Color24`, `Link24`, `Person24`, `PlugConnected24`, `SignOut24` |
| Misc | `ArrowClockwise24`, `Folder24`, `Info24`, `BookInformation24`, `Dismiss24` |

**Validation rules**: every `Symbol="…"` / `SymbolRegular.X` in the client resolves to a member of
`Wpf.Ui.Controls.SymbolRegular` (checked by a reflection audit); no bare glyph text.

## Entity: Automation ID Set (frozen + new)

Drives FR-009, SC-004. Existing IDs MUST NOT change; new interactive elements MUST add IDs.

```text
# Preserved (subset of the 021 frozen set that these surfaces own)
DarkThemeRadio, DashboardAskButton, DashboardLibraryButton, DashboardMetricDocuments,
DashboardMetricQueries, DashboardProfileCard, DashboardRefreshButton, DashboardRetryButton,
DashboardStatusBar, LightThemeRadio, NavAdmin, NavAsk, NavDashboard, NavHistory, NavLibrary,
NavMyDocs, NavSettings, RaggitBrandLabel, SignOutButton, ConnectionStatusValue, SignedInAsValue,
WorkstationUrlValue

# New (assigned during implementation)
DashboardTileHistory, DashboardTileMyDocs, DashboardTileAdmin,
SettingsRowAppearance, SettingsRowSignedInAs, SettingsRowConnectionStatus, SettingsRowSession
```

The Library and Ask tiles reuse the preserved `DashboardMetricDocuments` / `DashboardMetricQueries`
IDs (same navigate-to-library / navigate-to-ask behavior).

**Validation rules**: the post-change ID set is a superset of the pre-change set; none of the
preserved values is removed or renamed.

## State Transitions

Not applicable — no runtime state machine. Theme state is owned by the library's
`ApplicationThemeManager`; the only transition the client initiates remains the system-theme
application at startup (feature 021), with Light/Dark as explicit Settings overrides.
