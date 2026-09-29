# UI Contracts: Gallery-Style Design Language

**Feature**: `022-gallery-design-language` | **Date**: 2026-09-29 | **Plan**: [plan.md](../plan.md)

This is a desktop application with no external API, CLI, or wire contract. Its observable contracts
are the presentation invariants below, checked by the static checks and the UI-automation walkthrough
in [quickstart.md](../quickstart.md); no schema file is generated.

## C1 — Shell Contract (FR-001, FR-002, FR-003)

1. The window uses the library's modern Fluent chrome: a translucent backdrop
   (`WindowBackdropType`) with rounded corners, and starts centered on screen.
2. The navigation pane has a **visible toggle**; collapsing/expanding it reflows the content without
   clipping.
3. Navigating between destinations shows a page transition.

**Prohibited**: an unstyled/default window frame; a pane with no toggle; a hard-cut page swap.

## C2 — Navigation Contract (FR-005)

1. Destinations are listed in a single ordered group (no section headings), in the order Dashboard,
   Library, Ask, History, My Docs (plus Admin), with Settings pinned in the footer.
2. The administrator destination appears iff the signed-in user is an administrator.
3. The pane provides **no** search field (FR-004 withdrawn at the user's request).

**Prohibited**: a footer that loses Settings; an administrator destination shown to a
non-administrator.

## C3 — Theme Contract (FR-010, FR-011)

1. Zero hard-coded color literals (`#RRGGBB(AA)`, `Colors.*`, `Color.FromRgb`) in the changed
   surfaces.
2. All color references are `{ui:ThemeResource …}` / `{DynamicResource …}`.
3. The hero gradient uses only `SystemAccentColor*` stops and `TextOnAccentFillColorPrimaryBrush`
   text, so it adapts to Light, Dark, and High Contrast and follows the system accent.
4. Light, Dark, and High Contrast all render legible text, borders, and focus visuals.

**Prohibited**: a hero that hard-codes white/black; text over the accent that fails contrast in any
theme.

## C4 — Automation & Accessibility Contract (FR-009, FR-015)

1. Every automation ID on the affected surfaces present before the change remains intact.
2. Every new interactive element (nav search, tiles, rows) declares a stable automation ID.
3. Every primary action is keyboard reachable with a visible focus indicator.
4. Icon-only controls carry an accessible name and a matching tooltip.

**Prohibited**: a renamed/removed existing ID; a new interactive control without an ID; focus that
is invisible or trapped.

## C5 — Layout Contract (FR-013)

1. At 800×600 all primary content on the affected surfaces is reachable and unclipped.
2. Dashboard tiles **reflow** (wrap to fewer columns) rather than clip.
3. Long display names, roles, and URLs truncate gracefully instead of displacing controls.

**Prohibited**: a fixed-column tile grid that clips at the minimum size; horizontal scrollbars on the
primary surfaces at 800×600.

## C6 — Behavior-Freeze Contract (FR-014)

1. `RAGGit.Client.Core`, `RAGGit.Workstation.Api`, and `specs/*/contracts/api.yaml` are read-only.
2. No query, upload, citation, identity, or offline behavior changes.
3. No `ScrollViewer` wraps a virtualizing collection.

**Prohibited**: adding commands to `RAGGit.Client.Core` ViewModels; changing navigation semantics.

## C7 — Icon Contract (FR-012)

1. Icons render via `ui:SymbolIcon` + `SymbolRegular`.
2. Every referenced icon name is a **valid member** of `SymbolRegular` (runtime-validated; an invalid
   name compiles but throws `XamlParseException` at load).
3. No interactive content is a bare glyph character.

**Prohibited**: an icon name copied from the Fluent catalogue without validating it against 4.3.0.

## C8 — Consistency Contract (FR-007, FR-008)

1. Secondary text uses the library's secondary convention (`Appearance="Secondary"` or
   `TextFillColorSecondaryBrush`).
2. Dashboard destinations present as tiles (icon + title + description); Settings settings present as
   labelled rows.
3. Typography comes from the library's `Typography` (`FontTypography`), not ad-hoc `FontSize`.

**Prohibited**: mixing ad-hoc font sizes with library typography on the changed surfaces.
