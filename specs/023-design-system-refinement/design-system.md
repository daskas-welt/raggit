# Design System: RAGGit Desktop Client (T004/T026)

**Feature**: `023-design-system-refinement` | **Date**: 2026-09-29 | **Spec**: [spec.md](spec.md)

The single reference for the client's typography, colour, iconography and state conventions. New
pages **adopt** these; they do not re-declare them. Enforced by the audits in
[quickstart.md](quickstart.md).

## Shared styles (`src/RAGGit.Client.WPF/App.xaml`)

| Key | Target | Definition | Use |
|-----|--------|------------|-----|
| `PageTitleText` | `ui:TextBlock` | `FontTypography="Title"` | page header title |
| `PageSubtitleText` | `ui:TextBlock` | `FontTypography="Body"`, `Appearance="Secondary"`, `TextWrapping="Wrap"` | page header supporting line |
| `SectionHeaderText` | `ui:TextBlock` | `FontTypography="BodyStrong"` | section headings (Settings groups) |
| `SecondaryText` | `ui:TextBlock` | `Appearance="Secondary"` | supporting copy, table cells |
| `EmptyStateGlyph` | `ui:SymbolIcon` | `FontSize="40"`, centred, `TextFillColorSecondaryBrush` | empty-state imagery |
| `HeroAccentGradientBrush` | `LinearGradientBrush` | `SystemAccentColorPrimary` → `SystemAccentColorSecondary` | Dashboard hero (feature 022) |

## Type roles

`TitleLarge` hero · `Title` page title · `BodyStrong` section heading · `Body` labels/body ·
`Caption` meta. **No ad-hoc `FontSize` on `ui:TextBlock`**; `FontSize` is acceptable only on controls
that expose no `Typography` (`ui:Button`, `ComboBox`, `ui:SymbolIcon`).

## Colour roles

| Role | Token |
|------|-------|
| Primary text | library default |
| Secondary text | `TextFillColorSecondaryBrush` (via `SecondaryText` / `PageSubtitleText`) |
| Text on accent | `TextOnAccentFillColorPrimaryBrush` |
| Lower emphasis on accent | `TextOnAccentFillColorSecondaryBrush` |
| Decorative on accent | `TextOnAccentFillColorDisabledBrush` |
| Accent fill (interactive) | `{DynamicResource AccentFillColorDefaultBrush}` |
| Accent decoration (gradient, borders, glyphs) | `{ui:ThemeResource SystemAccentColorPrimaryBrush}` |
| Status | `SystemFillColorSuccess` / `Critical` / `Caution` / `Attention` / `Neutral` `…Brush` |
| Divider | `DividerStrokeColorDefaultBrush` |
| Chip surface | `ControlFillColorSecondaryBrush` |

**Rules**: zero hard-coded colours; **never** `Opacity=` as a colour substitute; never accent-coloured
text on an accent surface; `AccentFillColorDefaultBrush` only through `{DynamicResource}` (it is **not**
a `ui:ThemeResource` enum member and throws at load).
`Text/Tertiary` and `Text/Disabled` are **control states, not content** (per the component library's
own guidance) — content emphasis stops at Secondary.

## Icon scale & state

| Step | Size | Use |
|------|------|-----|
| Inline | 16 | status chips, beside Caption text |
| Control | 20 | navigation, buttons, icon-only actions, pager |
| Row | 24 | settings rows, list actions, tile chip glyphs |
| Feature | 32–44 | destination tiles, empty states (40) |

**State**: Regular = resting; `Filled="True"` = status/active/emphasis. The active navigation
destination uses the filled variant of its item glyph plus the pane's own selection indicator.

**Concepts** (one glyph each): Dashboard `Home24` · Library `Library24` · Ask `Chat24` ·
History `History24` · My Docs `Document24` · Admin `People24` · Settings `Settings24` ·
Refresh `ArrowClockwise24` · Upload `ArrowUpload24` · Browse `FolderOpen24` ·
Download `ArrowDownload24` · Delete `Delete24` · Copy `Copy24` · Send `Send24` ·
Pager `Chevron*20`.

**Documented exceptions**: the Dashboard hero watermark uses `BookInformation24` at `FontSize="96"`
(a background decoration, outside the control scale); double chevrons have no 24px variant, so the
pager standardises on 20.

## Page inventory

Every listed page header shows a title (concept: shared style) and a supporting line; every listed
table separates rows with `DividerStrokeColorDefaultBrush` and distinguishes its header row; every
listed empty state shows `EmptyStateGlyph` + title + hint.

| Page | Header | Table separation | Empty state |
|------|--------|------------------|-------------|
| Dashboard | hero + tagline | — | recent documents, recent questions |
| Library | title + line | ✅ rows + header | no documents |
| Ask | title + line | — | (chat empty state owned by `ChatControl`) |
| History | title + line | — | no queries |
| My Documents | title + line | — | no documents |
| Admin (People) | title + line | ✅ rows + header | no users |
| Query Detail | title + line | — | — |
| Settings | title + line | — | — |
| Login | title (centred card) | — | — |

## Behaviour-freeze

`RAGGit.Client.Core`, `RAGGit.Workstation.Api` and `specs/*/contracts/api.yaml` are untouched; the 22
frozen automation IDs remain (see [baseline.md](baseline.md)).
