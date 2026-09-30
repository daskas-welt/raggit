# Research: Design-System Refinement

**Feature**: `023-design-system-refinement` | **Date**: 2026-09-29 | **Plan**: [plan.md](plan.md)

Decisions resolving the plan's Technical Context. Library findings were verified against the installed
`Wpf.Ui.dll` 4.3.0 (assembly reflection + resource-key strings) and the WPF-UI Gallery's
Typography / Colors / Icons pages.

## Decision: Express the design system as shared styles in `App.xaml`

**Rationale**: Repeatable treatments were duplicated inline across pages (secondary foregrounds on
the Dashboard, Library and Login; page titles; empty states). WPF `Style` resources in
`Application.Resources` give one definition per treatment, satisfy FR-013/SC-004, and are the natural
place beside the existing `HeroAccentGradientBrush`. Styles to add: `PageTitleText`,
`PageSubtitleText`, `SectionHeaderText`, `SecondaryText`, `EmptyStateGlyph` (a planned `MetaText` style
was dropped — meta text is `Caption` + `SecondaryText`, so it needed no definition of its own).

**Alternatives considered**: per-page `Style` resources — rejected, they re-duplicate; a custom
`ResourceDictionary` file — rejected, `App.xaml` already holds the app-level brush and page styles
load fine from there.

## Decision: Page-header pattern = title + supporting line

**Rationale**: Library/History/My Documents/Admin/Ask/Settings opened with a bare title while the
Dashboard had a hero with a tagline. A title + one-line description is the Gallery's own page pattern
and gives the type ramp a purpose (FR-004). The Dashboard keeps its hero (its tagline lives there).

**Alternatives considered**: rely on the nav item label as context — rejected, it is not in the
content area and does not describe the surface.

## Decision: One secondary-text treatment; Tertiary/Disabled are control states, not content

**Rationale**: The Gallery's Colors page labels Text/Tertiary as "Pressed only (not accessible)" and
Text/Disabled as "Disabled only (not accessible)". So for content emphasis the only accessible level
below Primary is **Secondary**. Supporting copy therefore uses `Appearance="Secondary"`
(`TextFillColorSecondaryBrush`), and Tertiary/Disabled are reserved for control states. This also
means the earlier instinct to use Tertiary for timestamps would have been a legibility regression.

**Alternatives considered**: Tertiary for timestamps — rejected (not accessible for content);
opacity — rejected (see below).

## Decision: Never use `Opacity=` as a colour substitute

**Rationale**: Baseline had 20 `Opacity=` usages standing in for emphasis (0.7 on supporting copy,
0.9 on hero lines, 0.12 on the watermark). Opacity compounds with a token's own alpha and can fall
below legibility in High Contrast, and it hides the intent. Replacement roles: supporting copy →
`Appearance="Secondary"`; lower-emphasis text **on accent** → `TextOnAccentFillColorSecondaryBrush`;
decorative watermark → `TextOnAccentFillColorDisabledBrush`.

**Alternatives considered**: define new local opacity brushes — rejected, duplicates the library's
roles.

## Decision: Accent roles are explicit

**Rationale**: Two different tokens were doing "accent fill" (`SystemAccentColorPrimaryBrush` and
`AccentFillColorDefaultBrush`) and two were doing "text on accent"
(`AccentTextFillColorPrimaryBrush`, `TextOnAccentFillColorPrimaryBrush`). Note a trap verified during
feature 022: `AccentFillColorDefaultBrush` is **not** a `ui:ThemeResource` enum member, so it is only
reachable via `{DynamicResource}`; using it with `ui:ThemeResource` throws at load.
Rules: interactive accent **fill** → `{DynamicResource AccentFillColorDefaultBrush}`; accent
**decoration** (gradient, borders, glyphs) → `{ui:ThemeResource SystemAccentColorPrimaryBrush}`;
**text on accent** → `TextOnAccentFillColorPrimaryBrush`; accent-coloured **text on a light surface**
→ `AccentTextFillColorPrimaryBrush`.

**Alternatives considered**: keep both tokens — rejected, it makes "which accent" a coin flip and
invites the `ui:ThemeResource` trap.

## Decision: Icon size scale + filled-for-state

**Rationale**: Sizes had drifted (status 14, pager mixed 20/24, tiles 32, watermark 150). A four-step
scale with a stated purpose (16 inline · 20 nav/buttons · 24 rows/chip glyphs · 32–44 tiles/empty
states) removes the guesswork (FR-007). `SymbolIcon.Filled` is a real bool and `SymbolFilled` mirrors
the vocabulary, so stateful glyphs can be filled without swapping symbol names (FR-009).

> *Realised values (T044/T048, 2026-09-30): two of the purposes above moved a step in the shipped client
> — navigation renders at 16 (inline) and the Dashboard tiles at 24 (row), while the empty states stay 40
> and the pager keeps the 20 step by glyph family. [design-system.md](design-system.md) (Icon scale &
> state) is authoritative for the scale as built.*

**Alternatives considered**: swap to `SymbolFilled.*` names — rejected, doubles the name surface for
no gain when `Filled="True"` exists; mixed sizes for the pager — rejected, no 24px double-chevron
exists, so the scale's 20px step is the correct fallback.

## Decision: Active destination = explicit `SymbolIcon` per item + filled on selection

**Rationale**: `NavigationViewItem.Icon` is an `IconElement`, and `SymbolIcon : FontIcon : IconElement`
carries a settable `Filled`. Building each item with an explicit `SymbolIcon` (instead of relying on
the ctor's icon) lets a single `SelectionChanged` handler flip `Filled` for the selected item, on top
of the library's own selection indicator (FR-010).

**Alternatives considered**: `ItemContainerStyle` triggers — rejected, the icon is an `IconElement`
set per item, not a templated part; rebuilding the item collection on selection — rejected, it
disrupts the pane's own selection state.

## Decision: Table separation with a divider token; empty states with a glyph

**Rationale**: `DividerStrokeColorDefaultBrush` exists in the `ThemeResource` enum, so row separation
can be token-based (FR-011) rather than a hand-drawn line. For empty lists (FR-012) a
`ui:SymbolIcon` in `TextFillColorSecondaryBrush` above a `BodyStrong` title plus a `Secondary` hint
matches the tile language; verified glyphs `Document24`, `FolderOpen24`, `Search24`, `History24`,
`People24` (avoid `Inbox24` / `ClipboardText24` — they do not exist).

**Alternatives considered**: subtle zebra fills — rejected, they fight the card background in Dark;
icon-only empty state — rejected, a title plus next step is more actionable.

## Decision: Extend the existing static audits rather than add tooling

**Rationale**: Feature 022 established audits for colour literals, literal glyphs, `SymbolRegular`
names, `ThemeResource` keys and the automation-ID superset (all in quickstart.md). This feature adds
`Opacity=` (expect 0), ad-hoc `FontSize` on `ui:TextBlock` (expect 0) and a page-header presence
check, so the rules are enforced rather than aspirational.

**Alternatives considered**: rely on review — rejected, the drift being fixed happened under review.

## Design grounding sources

- Installed `Wpf.Ui.dll` 4.3.0: `IconElement`/`SymbolIcon.Filled`, `ThemeResource` members
  (`DividerStrokeColorDefaultBrush`, `SubtleFillColorSecondaryBrush`, `TextFillColorDisabledBrush`,
  `SystemFillColorCautionBrush`), `SymbolRegular` names.
- WPF-UI Gallery: Typography page (type ramp), Colors page (Text/Accent Text level annotations),
  Icons page (size families + "Is filled").
- `specs/023-design-system-refinement/design-system.md`: the canonical taxonomy, icon scale, state
  conventions and style keys owned by this feature.
- `src/RAGGit.Client.WPF` audit: icon (36), typography (130) and colour (80) usages.
