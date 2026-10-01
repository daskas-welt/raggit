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

**Outside the roles (feature `024-client-app-icon`)**: the application icon is the one place the client
uses a colour that is not a theme token — the product's first fixed brand colour `#0F6CBD`, owned by the
icon asset and the generator beside it (`src/RAGGit.Client.WPF/Assets/`). It is never used on a UI
surface, so the rules above still hold everywhere they apply.

## Icon scale & state

| Step | Size | Use | Realised by |
|------|------|-----|-------------|
| Inline | 16 | status chips, navigation rows, glyphs beside Caption text | `FontSize="16"` on the status chip; the navigation control's own style for nav glyphs |
| Control | 20 | pager and button-hosted glyphs | the glyph family the client picks (`Chevron*20` in `PaginationFooterControl`); the button style supplies the size |
| Row | 24 | settings rows, list and row actions, tile chip glyphs | `FontSize="24"` on the Dashboard tiles; the component library's `SymbolIcon` size for row and action glyphs |
| Feature | 40 | empty states | the `EmptyStateGlyph` shared style (`FontSize="40"`) |

**Realisation**: a glyph's size comes from an explicit `FontSize` where the client or a shared style sets
one — status chip 16, Dashboard tiles 24, `EmptyStateGlyph` 40, the hero watermark 96 (the documented
decoration exception) — and otherwise from the component library's control style, because the client sets
no size at all on navigation, pager, row or action glyphs. Measured live with UI Automation on
2026-09-30 (100% scaling): a `History24` glyph measures `24x26` in a Dashboard tile (explicit 24) and
`16x17` in a navigation row, and a settings-row glyph measures `24x26`; so navigation sits on the Inline
step and rows, tiles and actions on the Row step. Where the commitment is a glyph family rather than a
client-set size (the pager's `Chevron*20`), the family names the step and the control style sizes it.

**State**: Regular = resting; `Filled="True"` = status/active/emphasis. The active navigation
destination uses the filled variant of its item glyph plus the pane's own selection indicator.

### Status vocabulary

The document status indicator (`Components/StatusChip`) renders one chip per state at the inline (16)
step: the tone picks the glyph shape and the two colour roles the chip publishes for its glyph and its
label (`GlyphBrushKey` / `LabelBrushKey`). The two in-flight states — `Uploading` and `Queued` —
arrived with the background-ingest feature and use this same vocabulary.

| State | Tone | Glyph (`Filled="True"`) | Glyph colour role | Label colour role |
|-------|------|-------------------------|-------------------|-------------------|
| `Uploading` | InProgress | `ArrowSyncCircle24` | accent decoration (`SystemAccentColorPrimaryBrush`) | primary text |
| `Queued` | InProgress | `ArrowSyncCircle24` | accent decoration | primary text |
| `Indexing` | InProgress | `ArrowSyncCircle24` | accent decoration | primary text |
| `Ready` | Positive | `CheckmarkCircle24` | status (`SystemFillColorSuccessBrush`) | status |
| `Failed` | Error | `ErrorCircle24` | status (`SystemFillColorCriticalBrush`) | status |
| unknown | Neutral | `Circle24` | status (`SystemFillColorNeutralBrush`) | primary text |

**Rule**: the accent **fill** token (`AccentFillColorDefaultBrush`) is never a foreground — it exists to
sit behind text on accent. The two colour columns are independent. The **glyph** of a non-in-progress
state uses the `SystemFillColor*` status tokens and the glyph of an in-progress state uses the accent
**decoration** token; the **label** carries a status colour only for the two terminal states (`Ready`,
`Failed`) and every other label — including the neutral one — stops at primary text.

**Concepts** (one glyph each): Dashboard `Home24` · Library `Library24` · Ask `Chat24` ·
History `History24` · My Docs `Document24` · Admin `People24` · Settings `Settings24` ·
Refresh `ArrowClockwise24` · Upload `ArrowUpload24` · Browse `FolderOpen24` ·
Download `ArrowDownload24` · Delete `Delete24` · Copy `Copy24` · Send `Send24` ·
Pager `Chevron*20`.

**Concepts added by feature `025-fluent-system-icons`**: Sign in `ArrowEnterLeft24` · Sign out `SignOut24` ·
Create person `PersonAdd24` · Change role `PersonEdit24` · Reset password `KeyReset24` ·
Retry `ArrowClockwise24` (shares the reload concept with Refresh) · Load more `ArrowDown24` ·
Cancel `Dismiss24` · Close `Dismiss24` (shares the dismiss concept with Cancel) ·
Ask again `ArrowRepeatAll24` · Back `ArrowLeft24` · View `Eye24` · Sources `TextQuote24`.

**State glyphs added by `025`**: person active `Checkmark24` (Activate) / `Dismiss24` (Deactivate) ·
person lock `LockClosed24` (locked) / `LockOpen24` (not locked) · connection reachable
`PlugConnected24` / unavailable `PlugDisconnected24` · message severity `Info24` (informational) /
`CheckmarkCircle24` (success) / `Warning24` (warning) / `ErrorCircle24` (error).

The glyphs `025` adds set **no** `FontSize`; they take the component library's control size, exactly as
navigation, row and action glyphs already do. Every one of them is delivered **beside** its label — no
labelled control becomes icon-only (`016`'s decision stands), and the Ask page's repeated person
suggestion chips stay text-only.

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
| Login | title + supporting line (centred card) | — | — |

## Behaviour-freeze

This feature changed only `src/RAGGit.Client.WPF/`; `RAGGit.Client.Core`, `RAGGit.Workstation.Api` and
`specs/*/contracts/api.yaml` were untouched by it — the later background-ingest feature is what extended
the Core status vocabulary (`Uploading`/`Queued`) and its label/tone mapping. The 22 frozen automation
IDs remain (see [baseline.md](baseline.md)).
