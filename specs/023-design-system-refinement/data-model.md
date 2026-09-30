# Data Model: Design-System Refinement

**Feature**: `023-design-system-refinement` | **Date**: 2026-09-29 | **Plan**: [plan.md](plan.md)

No persisted data and no API contract. The "model" is the design system: its type roles, colour roles,
icon scale, state conventions, shared styles, and the page inventory that must conform.

## Entity: Type Role

Drives FR-002, FR-004, SC-002, SC-004. One role per text purpose.

| Role | Style / step | Used for |
|------|--------------|----------|
| Page title | `PageTitleText` = `FontTypography="Title"` | §1 page headers |
| Page subtitle | `PageSubtitleText` = `Body` + `Appearance="Secondary"` | one supporting line per page header |
| Section heading | `SectionHeaderText` = `FontTypography="BodyStrong"` | card/section headings |
| Body | `FontTypography="Body"` | labels, form labels, list text |
| Secondary | `SecondaryText` = `Appearance="Secondary"` | supporting copy |
| Meta | `Caption` + `SecondaryText` | timestamps, counts |
| Hero title | `FontTypography="TitleLarge"` + on-accent brush | Dashboard hero only |

**Validation rules**: no ad-hoc `FontSize` on `ui:TextBlock`; every primary page uses the page-title
**and** page-subtitle roles; `Tertiary`/`Disabled` are never used for content.

## Entity: Colour Role

Drives FR-003, FR-005, FR-006, SC-003.

| Role | Token | Notes |
|------|-------|-------|
| Primary text | library default (`TextFillColorPrimaryBrush`) | rest/hover |
| Secondary text | `TextFillColorSecondaryBrush` (or `Appearance="Secondary"`) | the only accessible content level below primary |
| Text on accent | `TextOnAccentFillColorPrimaryBrush` | titles on accent surfaces |
| Lower-emphasis on accent | `TextOnAccentFillColorSecondaryBrush` | hero supporting lines |
| Decorative on accent | `TextOnAccentFillColorDisabledBrush` | hero watermark |
| Accent fill | `{DynamicResource AccentFillColorDefaultBrush}` | interactive accent surfaces |
| Accent decoration | `SystemAccentColorPrimaryBrush` | gradient stops, borders, glyphs |
| Status | `SystemFillColorSuccess/Critical/Caution/Attention/Neutral` `…Brush` | status indicators |
| Divider | `DividerStrokeColorDefaultBrush` | table row separation |

**Validation rules**: zero hard-coded colour values; zero `Opacity=`; no accent-coloured text on an
accent surface; `AccentFillColorDefaultBrush` only via `{DynamicResource}`.

## Entity: Icon Scale & State

Drives FR-007, FR-008, FR-009, SC-005.

| Step | Size | Purpose | Realisation / examples |
|------|------|---------|------------------------|
| Inline | 16 | status chips, navigation rows, glyphs beside Caption/meta text | explicit `FontSize="16"` (`StatusChip`); the navigation control's own style |
| Control | 20 | pager and button-hosted glyphs | glyph family `Chevron*20` (`PaginationFooterControl`), sized by the button style |
| Row | 24 | settings rows, list/row actions, tile chip glyphs | `FontSize="24"` on the Dashboard tiles; the library's `SymbolIcon` size elsewhere (`Color24`, `Link24`, `Person24`) |
| Feature | 40 | empty states | `EmptyStateGlyph` (`FontSize="40"`) |

**State convention**: Regular = resting; `Filled="True"` = status/active/emphasis.

**Status vocabulary**: a document-status chip (`Components/StatusChip`) is one per state, painted with
the tone's colour roles — in-progress states (`Uploading`/`Queued`/`Indexing`) share the
`ArrowSyncCircle24` glyph with the accent **decoration** token (`SystemAccentColorPrimaryBrush`),
`Ready` uses `CheckmarkCircle24` with the success status token, `Failed` uses `ErrorCircle24` with the
critical status token, and labels stop at primary text except for those two terminal states. The full
table is in [design-system.md](design-system.md) (Status vocabulary).

**Validation rules**: every referenced name is a valid `SymbolRegular`/`SymbolFilled` member; one
symbol per concept; sizes drawn from the scale (documented fallback where a step has no variant). Where
the client sets no `FontSize`, the size is the component library's control style for that control — the
live-measured values are in [design-system.md](design-system.md) (Icon scale & state).

## Entity: Shared Style

Drives FR-013, SC-004.

| Style key | Target | Definition |
|-----------|--------|------------|
| `PageTitleText` | `ui:TextBlock` | `FontTypography="Title"` |
| `PageSubtitleText` | `ui:TextBlock` | `FontTypography="Body"`, `Appearance="Secondary"`, `TextWrapping="Wrap"` |
| `SectionHeaderText` | `ui:TextBlock` | `FontTypography="BodyStrong"` |
| `SecondaryText` | `ui:TextBlock` | `Appearance="Secondary"` |
| `EmptyStateGlyph` | `ui:SymbolIcon` | `FontSize="40"`, `HorizontalAlignment="Center"`, `Foreground="{ui:ThemeResource TextFillColorSecondaryBrush}"` |

**Validation rules**: the page-header and empty-state patterns are expressed through these styles;
adding a new page reuses them rather than re-specifying attributes.

## Entity: Page

Drives FR-004, FR-011, FR-012, SC-002, SC-006.

| Page | Header title + subtitle | Table separation | Empty state |
|------|------------------------|------------------|-------------|
| Dashboard | hero title + tagline (already) | — | recent documents, recent questions |
| Library | title + subtitle | rows + header | no documents |
| Ask | title + subtitle | — | (chat empty state owned by ChatControl) |
| History | title + subtitle | — | no questions |
| My Documents | title + subtitle | — | no documents |
| Admin (People) | title + subtitle | rows + header | no people |
| Settings | title + subtitle | — | — |
| Login | title + supporting line (centred card) | — | — |
| Query Detail | title + subtitle | — | — |

**Validation rules**: every listed header shows a title and a supporting line; every listed empty
state shows a glyph, a title and a next-step hint; every table shows row separation and a distinct
header row.

## Entity: Automation ID Set (frozen)

Drives FR-014, SC-008. The 22 IDs frozen by feature 022 MUST remain present; new interactive elements
(if any) add new IDs.

```text
RaggitBrandLabel, NavDashboard, NavLibrary, NavAsk, NavHistory, NavMyDocs, NavAdmin, NavSettings,
DashboardProfileCard, DashboardRefreshButton, DashboardMetricDocuments, DashboardMetricQueries,
DashboardLibraryButton, DashboardAskButton, DashboardStatusBar, DashboardRetryButton,
WorkstationUrlValue, SignedInAsValue, ConnectionStatusValue, LightThemeRadio, DarkThemeRadio,
SignOutButton
```

## State Transitions

The only selection state the client owns is the active navigation destination, expressed as the
selected item's filled glyph plus the library's selection indicator.

Document status is a server-side lifecycle the client renders rather than a client state machine:
`Uploading → Queued → Indexing → Ready | Failed` (background ingest). The per-state tone, glyph and
colour roles are in [design-system.md](design-system.md) (Status vocabulary).
