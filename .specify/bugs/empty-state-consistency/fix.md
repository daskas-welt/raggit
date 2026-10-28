# Bug Fix: Page-level empty states share one card surface

- **Slug**: empty-state-consistency
- **Fixed**: 2026-10-27
- **Assessment**: ./assessment.md
- **Status**: applied

## Summary

Every page-level empty state now uses one shared `EmptyStateCard` style (bordered, 24 px padded,
capped at 480 px) defined in `App.xaml`, instead of a mix of faint default cards, chrome-less bare
panels and inline padding values. Card styles that carry visibility triggers are `BasedOn` the shared
style so they keep WPF-UI's card template.

## Changes

| File | Change | Notes |
|------|--------|-------|
| `src/RAGGit.Client.WPF/App.xaml` | added `EmptyStateCard` style | `BasedOn="{StaticResource {x:Type ui:Card}}"`, `CardPaddingLarge`, `MaxWidth=480`, 1 px `SurfaceStrokeColorDefaultBrush` |
| `src/RAGGit.Client.WPF/Views/Pages/DashboardPage.xaml` | modified | Local trigger style is now `BasedOn EmptyStateCard`; inline padding/max-width/border setters removed |
| `src/RAGGit.Client.WPF/Views/Pages/LibraryPage.xaml` | modified | "No documents yet" + "No matching documents" use the shared style; no-records card gained `LibraryEmptyState` automation ID/name |
| `src/RAGGit.Client.WPF/Views/Pages/DocumentsMinePage.xaml` | modified | same pair; no-records card gained `DocumentsMineEmptyState` |
| `src/RAGGit.Client.WPF/Views/Pages/HistoryPage.xaml` | modified | same pair; no-records card gained `HistoryEmptyState` |
| `src/RAGGit.Client.WPF/Views/Pages/AdminUsersPage.xaml` | modified | "No people yet" + "No matching people" on the shared style; `Padding="20"` → style, 8 px gaps → 12 px, `BodyStrong` → `Title`; IDs `AdminEmptyState` / `AdminNoMatchState` |
| `tests/unit/ClientEmptyStateCardStyleTests.cs` | added test | static XAML guard: shared style exists, page card styles have `BasedOn`, page-level empty states reference `EmptyStateCard` |

## Diff Highlights (optional)

```xml
<!-- App.xaml -->
<Style
    x:Key="EmptyStateCard"
    TargetType="ui:Card"
    BasedOn="{StaticResource {x:Type ui:Card}}"
>
    <Setter Property="Padding" Value="{StaticResource CardPaddingLarge}" />
    <Setter Property="MaxWidth" Value="480" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="BorderBrush" Value="{ui:ThemeResource SurfaceStrokeColorDefaultBrush}" />
</Style>
```

```xml
<!-- LibraryPage.xaml: the no-match state keeps its trigger, not a replace-everything style -->
<ui:Card x:Name="LibraryNoMatchState" HorizontalAlignment="Center" VerticalAlignment="Center" ...>
    <ui:Card.Style>
        <Style TargetType="ui:Card" BasedOn="{StaticResource EmptyStateCard}">
            <Setter Property="Visibility" Value="Collapsed" />
            ...
```

## Tests Added or Updated

- `tests/unit/ClientEmptyStateCardStyleTests.cs`
  - `AppResources_DefineSharedEmptyStateCardBasedOnTheImplicitCardStyle` — pins the shared style and its
    `BasedOn` so the Fluent card template cannot be replaced away.
  - `PageCardStyles_KeepTheImplicitCardStyleAsBase` — every `Style TargetType="ui:Card"` under
    `Views/Pages` must declare `BasedOn` (the exact failure mode that made the no-match states bare).
  - `PageLevelEmptyStates_UseTheSharedEmptyStateCard` — every card carrying a page-level empty-state
    automation ID must reference `EmptyStateCard`; the test also asserts all nine IDs are still found.

## Local Verification

- Commands run: `dotnet csharpier check src/RAGGit.Client.WPF tests/unit/ClientEmptyStateCardStyleTests.cs` → 52 files checked, clean.
- Commands run: `dotnet build src/RAGGit.Client.WPF/RAGGit.Client.WPF.csproj -c Release -p:Platform=x64` → succeeded, 0 warnings / 0 errors.
- Commands run: `dotnet test tests/unit/RAGGit.Tests.Unit.csproj -c Release --filter "FullyQualifiedName~ClientEmptyStateCardStyleTests"` → 3/3 passed.
- Commands run: `dotnet test tests/unit/RAGGit.Tests.Unit.csproj -c Release --no-build` → 557/557 passed.
- Manual checks: none — a signed-in client session against a live API is required to see the empty
  states, and none was used for this fix. `winapp ui` can now target the new automation IDs
  (`LibraryEmptyState`, `DocumentsMineEmptyState`, `HistoryEmptyState`, `AdminEmptyState`,
  `AdminNoMatchState`) plus the four pre-existing ones.

## Deviations from Assessment

- **Added automation IDs/names** to the three "no records" cards and both Admin states. The assessment
  assumed the IDs already existed for verification, but only `DashboardEmptyState` and the three
  `*NoMatchState` cards had one. The guard test keys off these IDs rather than matching title text,
  because a text-based search also matches outer cards that merely contain an empty state (it flagged
  the Admin list card during development).
- **`MaxWidth=480` applied to every page-level empty state**, resolving the assessment's open question
  toward its preferred remediation. The Admin cards were narrower before; visual check still pending.
- **`PageHeaderCard` left untouched**: the probe confirmed it has the same template-less problem, but the
  assessment scoped this bug to empty states and flagged the header as a separate decision.

## Follow-ups

- Decide whether `PageHeaderCard` gets `BasedOn` too (same root cause; changes every page header).
- Decide whether nested placeholders should match: Dashboard recent documents/questions panels,
  `QueryDetailPage` "No citations", `ChatControl` start state, `UploadDialog` "No files yet".
- If empty states keep growing, consider the deferred `EmptyStateControl` (glyph + title + body +
  optional action) to remove the copied seven-row grids.
- Manual visual pass on all five pages in both empty variants, especially Admin at the ≤720 px
  responsive breakpoint after gaining `MaxWidth=480`.
