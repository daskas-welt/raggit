# Design Language: Gallery-Style Recipes (T003/T004)

**Feature**: `022-gallery-design-language` | **Date**: 2026-09-29 | **Plan**: [plan.md](plan.md)

The concrete recipes US1–US3 apply, seeded from [data-model.md](data-model.md) and verified against
WPF-UI 4.3.0. Implementation must follow these; `/speckit.plan` chose them in
[research.md](research.md).

## Ownership & boundaries (T004)

- Navigation **grouping and search** are implemented in
  `src/RAGGit.Client.WPF/ViewModels/MainWindowViewModel.cs` — the **WPF project**, not
  `RAGGit.Client.Core`.
- `RAGGit.Client.Core`, `RAGGit.Workstation.Api`, and `specs/*/contracts/api.yaml` are **read-only**
  (FR-014, C6). No commands are added to Core ViewModels; tile/row activation reuses code-behind
  handlers (`CardAction` derives from `ButtonBase`, so it exposes `Click`).

## Hero recipe (US2)

A `Border` with rounded corners and the shared gradient brush; text in the on-accent brush; a
low-opacity `ui:SymbolIcon` watermark for depth. **No color literals** (C3).

```xml
<!-- App.xaml resources (T010) -->
<LinearGradientBrush x:Key="HeroAccentGradientBrush" StartPoint="0,0" EndPoint="1,1">
    <GradientStop Offset="0" Color="{DynamicResource SystemAccentColorPrimary}" />
    <GradientStop Offset="1" Color="{DynamicResource SystemAccentColorSecondary}" />
</LinearGradientBrush>
```

```xml
<!-- DashboardPage.xaml hero -->
<Border CornerRadius="12" Background="{StaticResource HeroAccentGradientBrush}" Padding="32,24" MinHeight="180">
  <Grid>
    <ui:SymbolIcon Symbol="BookInformation24" FontSize="96"
                   HorizontalAlignment="Right" VerticalAlignment="Center"
                   Foreground="{ui:ThemeResource TextOnAccentFillColorDisabledBrush}" />
    <StackPanel VerticalAlignment="Center">
      <ui:TextBlock FontTypography="TitleLarge" Foreground="{ui:ThemeResource TextOnAccentFillColorPrimaryBrush}" Text="Welcome back" />
      <ui:TextBlock Margin="0,4,0,0" FontTypography="Body"  Foreground="{ui:ThemeResource TextOnAccentFillColorPrimaryBrush}" Text="Your library at a glance" />
      <ui:TextBlock Margin="0,4,0,0" FontTypography="Caption" Foreground="{ui:ThemeResource TextOnAccentFillColorPrimaryBrush}" Text="{Binding RoleContext}" />
    </StackPanel>
  </Grid>
</Border>
```

## Tile recipe (US2)

`ui:CardAction` (base type `System.Windows.Controls.Primitives.ButtonBase` → `Click`), icon + title +
one-line description, laid out in a **wrapping** panel so it reflows at 800×600 (C5).

```xml
<ui:CardAction AutomationProperties.AutomationId="DashboardTileLibrary"
                AutomationProperties.Name="Open the library"
                Click="OnLibraryClicked" IsChevronVisible="False" Margin="0,0,8,8">
  <Grid>
    <Grid.ColumnDefinitions>
      <ColumnDefinition Width="Auto" />
      <ColumnDefinition Width="*" />
    </Grid.ColumnDefinitions>
    <ui:SymbolIcon Grid.Column="0" Symbol="Library24" FontSize="32"
                   Foreground="{ui:ThemeResource AccentFillColorDefaultBrush}"
                   VerticalAlignment="Center" Margin="16,0,12,0" />
    <StackPanel Grid.Column="1" Margin="0,14" VerticalAlignment="Center">
      <ui:TextBlock FontTypography="BodyStrong" Text="Library" />
      <ui:TextBlock Appearance="Secondary" TextTrimming="CharacterEllipsis"
                    Text="Browse and upload documents" />
    </StackPanel>
  </Grid>
</ui:CardAction>
```

Layout: `ItemsControl` (or a `Grid`) with a `WrapPanel` panel; tiles use a fixed `Width` (e.g. 320)
and `Margin` so they wrap to one column at 800×600.

## Settings row recipe (US3)

`ui:CardControl` exposes `Icon`, `Header`, `Content` — the Gallery's settings-row pattern.

```xml
<ui:CardControl AutomationProperties.AutomationId="SettingsRowAppearance" Margin="0,0,0,12">
  <ui:CardControl.Icon>
    <ui:SymbolIcon Symbol="Color24" />
  </ui:CardControl.Icon>
  <ui:CardControl.Header>
    <StackPanel>
      <ui:TextBlock FontTypography="Body" Text="App theme" />
      <ui:TextBlock Appearance="Secondary" Text="Select which app theme to display" />
    </StackPanel>
  </ui:CardControl.Header>
  <!-- Control on the right: the existing light/dark radios (IDs preserved) -->
</ui:CardControl>
```

Section headings: `<ui:TextBlock FontTypography="BodyStrong" Text="Appearance &amp; behavior" />`
with `Margin="0,0,0,8"`.

## Navigation recipe (US1)

- Pane: `IsPaneToggleVisible="True"`, `OpenPaneLength="300"`, `Transition="FadeInWithSlide"`,
  `IsTopSeparatorVisible="False"`, `IsFooterSeparatorVisible="False"`, `FrameMargin="0"`,
  `IsBackButtonVisible="Collapsed"`.
- Items: a single ordered `MenuItems` list (Dashboard, Library, Ask, History, My Docs, plus Admin for
  administrators); Settings stays in `FooterMenuItems`. Section headings
  (`ui:NavigationViewItemHeader`) and the search field (`AutoSuggestBox`) were removed at the user's
  request.

## Conventions

- **Secondary text**: `Appearance="Secondary"` on `ui:TextBlock` (fall back to
  `Foreground="{ui:ThemeResource TextFillColorSecondaryBrush}"` only on non-`TextBlock` elements such
  as `ui:SymbolIcon`, which have no `Appearance`).
- **Typography**: `ui:TextBlock` + `FontTypography` (`TitleLarge`, `Title`, `BodyStrong`, `Body`,
  `Caption`); no ad-hoc `FontSize` on text. (`FontSize` remains acceptable on controls that expose no
  typography — `ui:Button`, `ComboBox`, `ui:SymbolIcon`.)
- **Color**: only `{ui:ThemeResource …}` / `{DynamicResource …}`; zero literals (C3).

The client-wide design system — colour-token roles, the icon size scale, state conventions and the
shared styles — is owned by feature `023`; see
[`specs/023-design-system-refinement/design-system.md`](../023-design-system-refinement/design-system.md).

## Valid icon names (verified against 4.3.0)

```text
Home24, Library24, Chat24, History24, Document24, People24, Settings24, Search24,
Color24, Link24, Person24, PlugConnected24, SignOut24, ArrowClockwise24, Folder24, Info24,
BookInformation24, Dismiss24
```

Avoid `ShieldPerson24` (does not exist). An invalid name compiles but throws `XamlParseException` at
load (C7).

## Automation IDs

**Preserved** (must not change — see [baseline-ids.md](baseline-ids.md)):
`RaggitBrandLabel`, `NavDashboard`, `NavLibrary`, `NavAsk`, `NavHistory`, `NavMyDocs`, `NavAdmin`,
`NavSettings`, `DashboardProfileCard`, `DashboardRefreshButton`, `DashboardMetricDocuments`,
`DashboardMetricQueries`, `DashboardLibraryButton`, `DashboardAskButton`, `DashboardStatusBar`,
`DashboardRetryButton`, `WorkstationUrlValue`, `SignedInAsValue`, `ConnectionStatusValue`,
`LightThemeRadio`, `DarkThemeRadio`, `SignOutButton`.

**New**: `DashboardTileHistory`, `DashboardTileMyDocs`, `DashboardTileAdmin`,
`SettingsRowAppearance`, `SettingsRowSignedInAs`, `SettingsRowConnectionStatus`, `SettingsRowSession`.

The Library and Ask tiles intentionally reuse the preserved `DashboardMetricDocuments` /
`DashboardMetricQueries` IDs because they carry the same navigate-to-library / navigate-to-ask
behavior (preserving behavior, not just the ID).

**Code-behind `x:Name`s that must survive the Settings restructure** (referenced by
`SettingsPage.xaml.cs`): `WorkstationUrlText`, `SignedInAsText`, `ConnectionStatusText`,
`LightThemeRadio`, `DarkThemeRadio`.
