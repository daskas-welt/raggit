# Baseline: Affected Surfaces (T002)

**Feature**: `022-gallery-design-language` | **Captured**: 2026-09-29 | **Plan**: [plan.md](plan.md)

Pre-change baseline for the three affected surfaces. T018 asserts the post-change automation-ID set
is a superset of the IDs recorded here (SC-004, C4).

## Baseline build / format (T001)

- `WPF-UI` 4.3.0 + `WPF-UI.DependencyInjection` 4.3.0 referenced in `src/RAGGit.Client.WPF/RAGGit.Client.WPF.csproj`.
- `dotnet build RAGGit.sln -c Release -p:Platform=x64` → succeeded, 0 warnings / 0 errors.
- `dotnet csharpier check .` → clean (241 files).

## Baseline automation IDs

**Shell — `src/RAGGit.Client.WPF/Views/MainWindow.xaml`** (1 literal):

```text
RaggitBrandLabel
```

**Shell navigation — `src/RAGGit.Client.WPF/ViewModels/MainWindowViewModel.cs`** (set in code, 7):

```text
NavDashboard, NavLibrary, NavAsk, NavHistory, NavMyDocs, NavAdmin, NavSettings
```

**Dashboard — `src/RAGGit.Client.WPF/Views/Pages/DashboardPage.xaml`** (8):

```text
DashboardProfileCard, DashboardRefreshButton, DashboardMetricDocuments, DashboardMetricQueries,
DashboardLibraryButton, DashboardAskButton, DashboardStatusBar, DashboardRetryButton
```

**Settings — `src/RAGGit.Client.WPF/Views/Pages/SettingsPage.xaml`** (6):

```text
WorkstationUrlValue, SignedInAsValue, ConnectionStatusValue, LightThemeRadio, DarkThemeRadio,
SignOutButton
```

**Bound IDs elsewhere in the client** (from feature 021; not on these surfaces, must also survive):

```text
{Binding MessageCitationsAutomationId}, {Binding SourcesAutomationId},
{Binding RemoveAutomationId, Mode=OneWay}
```

Total baseline on the affected surfaces: **22** (15 literal + 7 nav).

## Baseline static checks

| Check | Result |
|-------|--------|
| C3 color literals (`#[0-9A-Fa-f]{6,8}`/`Color.FromRgb`/`Colors.`) | clean (0) |
| C7 literal glyph content (`Content="[«‹›»←→↑↓]"`) | clean (0) |
| C7 `SymbolRegular` icon-name audit | clean (0 invalid names) |

The affected surfaces start clean; the post-change checks must stay clean (T017) and the ID set must
be a superset of the 22 above (T018).
