# UI Contracts: Design-System Refinement

**Feature**: `023-design-system-refinement` | **Date**: 2026-09-29 | **Plan**: [plan.md](../plan.md)

Desktop application, no external API. The observable contracts are the presentation invariants below,
checked by the static audits and screenshot review in [quickstart.md](../quickstart.md).

## D1 — Typography Contract (FR-002, FR-004, SC-002, SC-004)

1. Every primary page header shows a **title** and a **supporting line**, both via shared styles
   (`PageTitleText`, `PageSubtitleText`).
2. Supporting text uses **one** treatment (`Appearance="Secondary"`); no page's supporting text is
   dimmer than another's.
3. Type steps come from the documented ramp (`Title`, `TitleLarge`, `BodyStrong`, `Body`, `Caption`);
   **no ad-hoc `FontSize` on `ui:TextBlock`**.
4. `Tertiary`/`Disabled` levels are **not** used for content.

**Prohibited**: a page with a title but no supporting line; two different secondary treatments;
a `FontSize` on a `ui:TextBlock`.

## D2 — Colour Contract (FR-003, FR-005, FR-006, SC-003)

1. Zero hard-coded colour values; all colour from theme tokens.
2. **No `Opacity=` anywhere** — emphasis comes from token roles.
3. Text on an accent surface uses `TextOnAccentFillColor*`, never `AccentTextFillColor*`.
4. Accent roles: interactive fill → `{DynamicResource AccentFillColorDefaultBrush}`; decoration →
   `SystemAccentColorPrimaryBrush`.
5. Light, Dark and High Contrast all render legibly.

**Prohibited**: `Opacity=` used to dim text; `AccentFillColorDefaultBrush` written as
`ui:ThemeResource` (it is not an enum member and throws at load); accent-coloured text on accent.

## D3 — Icon Contract (FR-007, FR-008, SC-005)

1. Icon sizes are drawn from the scale: **16** inline (status chips, navigation) · **20** control
   (pager and button-hosted glyphs, a glyph-family choice) · **24** rows (settings rows, list actions,
   tile chip glyphs) · **40** empty states. A step is either an explicit `FontSize` or the size the
   component library's control style supplies where the client sets none.
2. One symbol per product concept (Library, Ask, History, My Documents, Admin, Settings, Refresh,
   Upload, Download, Delete, Copy, Send, Browse).
3. Every referenced name is a valid `SymbolRegular`/`SymbolFilled` member.
4. A concept whose step has no variant documents its fallback (e.g. double chevrons at 20).

**Prohibited**: an unvalidated icon name (compiles, throws at load); two symbols for one concept;
an off-scale size.

## D4 — State Contract (FR-009, FR-010, SC-007)

1. Status indicators distinguish states by **colour and glyph**, using `Filled="True"` where the
   resting glyph would read ambiguous.
2. The active navigation destination is visually distinguishable from the rest in all three themes
   (library selection indicator **plus** a filled glyph).

**Prohibited**: a status conveyed by colour alone; an active destination indistinguishable in High
Contrast.

## D5 — Data-Surface Contract (FR-011, FR-012, SC-006)

1. Table rows are visually separated (`DividerStrokeColorDefaultBrush`) so a row can be followed
   across columns; the header row is distinguishable from data rows.
2. A list with no items shows an empty state with a **glyph**, a **title** and a **next-step hint**.
3. Separators and empty-state imagery survive High Contrast and 800×600 without clipping.

**Prohibited**: an unseparated dense table; a bare sentence where an empty state belongs.

## D6 — Shared-Style Contract (FR-013, SC-004)

1. The page-header and empty-state patterns are expressed once as `App.xaml` styles and reused.
2. A new page adopts the styles rather than re-specifying attributes.

**Prohibited**: re-declaring a page-header or empty-state treatment inline per page.

## D7 — Behaviour-Freeze Contract (FR-016)

1. `RAGGit.Client.Core`, `RAGGit.Workstation.Api`, and `specs/*/contracts/api.yaml` are read-only.
2. No query, upload, citation, identity, offline, or navigation **semantics** change (visual
   emphasis only).
3. Every preserved automation ID remains.

**Prohibited**: adding commands to Core ViewModels; changing which destination a nav item opens.
