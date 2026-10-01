# Research: Dashboard Redesign — "Work overview"

**Feature**: `027-dashboard-redesign` | **Date**: 2026-10-01 | **Plan**: [plan.md](plan.md)

## Decision: Compact header instead of the tall hero

**Decision**: Replace the 170-DIP gradient hero ("Welcome back" banner) with a compact
header: the page title, a supporting line, and the profile card, refresh control, and
labeled quick actions on the right.

**Rationale**: The hero consumes the first screen for branding that adds no working
information, pushing the panels below the fold. A header built from the shared
`PageTitleText`/`PageSubtitleText` styles matches every other page and leaves room for
the numbers users actually open the dashboard to see.

**Alternatives considered**: keep a shorter hero (still spends vertical space on
decoration and keeps a one-off header treatment); move the profile into a settings page
(breaks the signed-in-context convention the frozen `DashboardProfileCard` ID exists
for).

## Decision: Six metric cards instead of the inline strip

**Decision**: Show Documents, Ready, Indexing, Failed, Questions, and Mine as six
clickable cards (icon + count + label), replacing the "·"-separated inline text strip.
Each card navigates to the surface it describes.

**Rationale**: The strip is read-only text at caption size with no affordance and hides
two ingestion states entirely. Cards make every number actionable and finally surface
the indexing/failed counts users need when an upload seems stuck. All six values already
exist on the view model, so this is binding, not new data.

**Alternatives considered**: keep the strip and add two more segments (preserves a
non-actionable, low-legibility treatment); a metrics table (heavier than the glanceable
overview the spec asks for).

## Decision: Retire the tiles; keep their IDs as labeled header buttons

**Decision**: Remove the five large navigation-duplicating tiles. History, My Docs, and
Admin survive as labeled header quick-action buttons that keep their existing automation
IDs (`DashboardTileHistory`, `DashboardTileMyDocs`, `DashboardTileAdmin`) with unchanged
navigation and meaning; Library and Ask are reached through the metric cards and the
panel buttons that already carry `DashboardMetricDocuments`, `DashboardMetricQueries`,
`DashboardLibraryButton`, and `DashboardAskButton`.

**Rationale**: The tiles duplicate the left navigation at large size while pushing real
content down. Keeping the frozen IDs on the replacement buttons preserves the automation
contract and proves no destination was lost in the redesign.

**Alternatives considered**: keep the tiles below the cards (preserves the duplication
and the page length the redesign removes); drop the IDs with the tiles (breaks the
frozen automation contract).

## Decision: Real `ui:Card` panels instead of hand-rolled Borders

**Decision**: Replace the two hand-rolled `<Border>`-as-card panels with real `ui:Card`
controls, per the Fluent-only rule.

**Rationale**: The current XAML carries an inline comment admitting the panels avoid
`ui:Card` because its style centres content when siblings differ in height. Centring is a
layout detail solvable with alignment and content templates; keeping bespoke Border
panels preserves a second card treatment on exactly the page the design system is meant
to unify. The Fluent rule (AGENTS.md) settles it: `ui:Card` where a Fluent equivalent
exists.

**Alternatives considered**: keep the Borders (violates the Fluent-only rule and keeps
two card treatments); restyle the shared Card control itself (out of scope — would touch
every page).

## Decision: Stack the panels at ≤720 DIPs content width

**Decision**: Panels sit side by side at wide content widths and stack below each other
when the page content width is 720 DIPs or less, owned by the page layer.

**Rationale**: 720 DIPs is the established sibling-feature breakpoint (026 uses it for
the same side-by-side→stacked adaptation) and keeps the minimum 800×600 window usable
without squeezing either panel. Page-owned width handling keeps transient layout state
out of the shared view model, which stays unchanged.

**Alternatives considered**: a fixed two-column layout at all widths (squeezes content
at 800×600); wrapping panels in a generic wrap panel (loses the deterministic
side-by-side vs stacked contract the spec requires).

## Verification decisions

- Run the 023 static audits (no hard-coded colours, no `Opacity=`, no ad-hoc
  `ui:TextBlock FontSize`, valid `SymbolRegular`/`ThemeResource` keys, frozen IDs) over
  the redesigned page.
- Walk the page with `winapp ui` at a wide size and at 800×600, cycling Light / Dark /
  High Contrast, confirming header, six cards, both panels, the whole-dashboard empty
  state, last-known-good on failed refresh, and keyboard/screen-reader reachability.
- Preserve all 11 frozen Dashboard IDs; add four stable new IDs for the Ready, Indexing,
  Failed, and Mine cards.
