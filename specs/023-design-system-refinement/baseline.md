# Baseline: Design-System Refinement (T002)

**Feature**: `023-design-system-refinement` | **Captured**: 2026-09-29 | **Plan**: [plan.md](plan.md)

Pre-change state this feature is measured against (captured before implementation; feature `022` owns
only the shell spacing, navigation content and search removal).

## Build / format (T001)

- `WPF-UI` 4.3.0 referenced in `src/RAGGit.Client.WPF/RAGGit.Client.WPF.csproj`.
- `dotnet build RAGGit.sln -c Release -p:Platform=x64` → succeeded, 0 warnings / 0 errors.
- `dotnet csharpier check .` → clean (241 files).

## Page headers

| Page | Title present | Supporting line present |
|------|---------------|-------------------------|
| Dashboard | ✅ (hero) | ✅ (hero tagline) |
| Library | ✅ | ❌ |
| Ask (`QueryPage`) | ✅ | ❌ |
| History | ✅ | ❌ |
| My Documents | ✅ | ❌ |
| Admin (People) | ✅ | ❌ |
| Settings | ✅ | ❌ |
| Query Detail | ✅ | ❌ |
| Login | ✅ (centred card) | ❌ (by design) |

## Tables

| Table | Row separation | Header row distinct |
|-------|----------------|---------------------|
| Library (documents) | ❌ | ❌ |
| Admin (people) | ❌ | ❌ |

## Empty states

| Surface | Glyph | Title | Hint |
|---------|-------|-------|------|
| Dashboard — recent documents | ❌ | ✅ | ✅ |
| Dashboard — recent questions | ❌ | ✅ | ✅ |
| Library — no documents | ❌ | ✅ | ✅ |
| History — no queries | ❌ | ✅ | ✅ |
| My Documents — no documents | ❌ | ✅ | ✅ |
| Admin — no users | ❌ | ✅ | ✅ |
| Chat (owned by `ChatControl`) | — | — | — (out of scope) |

## Static audits (baseline)

| Audit | Baseline |
|-------|----------|
| Hard-coded colour literals | 0 |
| `Opacity=` | 0 (the 20 baseline usages were removed by this feature) |
| Invalid `SymbolRegular` names | 0 |
| Invalid `{ui:ThemeResource}` keys | 0 |
| Automation IDs (frozen set) | 22 present |

## Automation ID set (frozen, must remain a superset)

```text
RaggitBrandLabel, NavDashboard, NavLibrary, NavAsk, NavHistory, NavMyDocs, NavAdmin, NavSettings,
DashboardProfileCard, DashboardRefreshButton, DashboardMetricDocuments, DashboardMetricQueries,
DashboardLibraryButton, DashboardAskButton, DashboardStatusBar, DashboardRetryButton,
WorkstationUrlValue, SignedInAsValue, ConnectionStatusValue, LightThemeRadio, DarkThemeRadio,
SignOutButton
```
