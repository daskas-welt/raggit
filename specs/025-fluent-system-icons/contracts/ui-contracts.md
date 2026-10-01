# UI Contracts: Fluent System Icons Across the Client

**Feature**: `025-fluent-system-icons` | **Date**: 2026-09-30 | **Plan**: [plan.md](../plan.md)

Desktop application, no external API. The observable contracts are the iconography invariants below,
checked by the static audits and the screenshot review in [quickstart.md](../quickstart.md).

## F1 — Vocabulary & Coverage (FR-001, FR-002, FR-003, SC-001, SC-002)

1. Every icon is drawn from the Fluent system icon set the component library already ships; no second
   icon font or package is introduced.
2. Every action control whose meaning has an agreed symbol displays that glyph (the action-surface table
   in [data-model.md](../data-model.md)).
3. Each concept uses exactly one glyph on every surface it appears on; the documented pairings
   (Refresh/Retry, Cancel/Close, the status reuses) are single concepts, not duplicates.
4. Every referenced glyph is a valid member of the library's `SymbolRegular` enum.

**Prohibited**: a raw text character standing in for an icon; two glyphs for one concept; a name that is
not in the enum (it compiles but throws at load); a new icon package or font.

## F2 — Size Scale (FR-005, SC-006)

1. Every glyph is drawn at a step from the documented scale (inline 16 · control 20 · row 24 · feature
   40); new action glyphs are `*24` row/control glyphs.
2. No ad-hoc `FontSize` is introduced for an icon; sizes come from the library's control style or a
   documented shared style.
3. Icon-only buttons keep their existing hit targets; adding a glyph does not shrink a target below the
   `016` ≥44px rule.

**Prohibited**: an off-scale size; a glyph sized to fit by giving it a one-off `FontSize`.

## F3 — Colour & Theme (FR-006, FR-008, SC-004, SC-006)

1. Every glyph takes its colour from a theme token role; no hard-coded colour and no `Opacity=`.
2. The accent **fill** token is never a glyph foreground; accent decoration uses
   `SystemAccentColorPrimaryBrush` (the `023` rule).
3. All glyphs remain visible and correctly contrasted in Light, Dark and High Contrast.

**Prohibited**: a literal colour on a glyph; simulated dimming; an icon that disappears in High
Contrast.

## F4 — Accessibility (FR-007, SC-005)

1. Every icon-only control exposes an accessible name and a tooltip describing its action.
2. Every icon-plus-label control keeps the accessible name carried by its label; the glyph never becomes
   the name.
3. Per-row controls keep their per-user accessible names and tooltips.

**Prohibited**: a glyph alone as the accessible name; dropping a tooltip from an icon-only control;
regressing a per-row label.

## F5 — State (FR-004, SC-003)

1. Every stateful surface distinguishes its states by glyph **and** colour; no state is signalled by
   colour or word alone.
2. A stateful glyph is visually distinct from a resting glyph and from the other state's glyph.
3. The active-navigation and status-chip conventions `023` set are preserved.

**Prohibited**: a state shown only by colour; the same glyph for two different states on one surface; a
lock/active glyph that reads as an action when the surface is display-only.

## F6 — Appropriate Placement (FR-009, FR-012)

1. Icons appear on action controls and state indicators only — not on plain content, table column
   headers, numeric page buttons, the pager ellipsis, free-text fields, or theme radio options.
2. The Ask page's repeated "did you mean?" person suggestion chips stay text-only (clarified
   2026-09-30).
3. Every labelled control keeps its label; no control is converted to icon-only by this feature.

**Prohibited**: decorating a surface where the glyph adds no meaning; a repeated identical glyph on a
row of content chips; removing a control's label.

## F7 — Documentation & Behaviour Freeze (FR-010, FR-011, FR-013, SC-007, SC-008, SC-009)

1. The design-system reference (`specs/023-design-system-refinement/design-system.md`) is extended with
   every new concept→glyph entry and any new size guidance, in place — one vocabulary, one owner.
2. Every automation identifier frozen by `022`/`023` (and later features) remains present and unchanged;
   new interactive elements receive stable identifiers.
3. No change to behaviour, commands, workflows, the shared behaviour layer, the workstation service, or
   any contract; the existing suites pass with zero assertion changes and the formatter gate is clean.

**Prohibited**: a second vocabulary document; renaming or removing an automation identifier; changing a
command, binding or navigation target in the name of an icon.
