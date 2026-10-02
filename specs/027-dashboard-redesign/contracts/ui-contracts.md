# UI Contracts: Dashboard Redesign — "Work overview"

**Feature**: `027-dashboard-redesign` | **Date**: 2026-10-01 | **Plan**: [plan.md](../plan.md)

Desktop presentation only, no external API. The observable contracts are the layout,
navigation, empty-state, identity, and theme invariants below, checked by the static
audits and the screenshot walkthrough in [quickstart.md](../quickstart.md).

## U1 — Compact header (FR-001, SC-001, SC-004)

1. The page opens with a compact header: title "Dashboard", a supporting line, and on
   the right the profile card, a refresh control, and labeled History, My Docs, and
   Admin quick actions.
2. The Admin quick action is visible to administrators only; non-administrators are
   offered no Admin action.
3. No tall gradient hero is present; the headline numbers are visible without scrolling
   at the minimum supported window size.

**Prohibited**: a decorative banner above the header; an unlabeled icon-only quick
action; an Admin action visible to non-administrators.

## U2 — Metric cards (FR-002, FR-003, FR-004, SC-001, SC-002)

1. Six cards are shown — Documents, Ready, Indexing, Failed, Questions, Mine — each
   with an icon, a count, and a label.
2. Activation navigates: Documents, Ready, Indexing, and Failed to the Library;
   Questions to Ask; Mine to My Docs.
3. Indexing and failed counts are visible on the dashboard whenever the library is
   non-empty; a zero card still renders and still navigates.
4. On a failed refresh after a successful load, the last-known numbers stay visible
   next to the error status instead of clearing.
5. Until a load has completed successfully (`HasLoaded` false), the metric cards and
   the library summary stay hidden — an unloaded dashboard presents no numbers,
   including its initial zeros, as loaded data.

**Prohibited**: a card with no destination; a read-only metric strip; hiding the
ingestion states; clearing numbers when a refresh fails.

## U3 — Panels and responsive layout (FR-005, FR-006, SC-004)

1. Recent documents and Recent questions panels are real Fluent card controls sitting
   side by side at wide content widths.
2. At page content widths of 720 DIPs or less the panels stack below each other; both
   remain reachable without clipping at 800×600.
3. Each panel shows its content list, or its own distinct empty state when it has no
   content — never a blank list.

**Prohibited**: a fixed two-column layout at all widths; hand-rolled Border-as-card
panels; a clipped or unreachable panel at the minimum window size.

## U4 — Whole-dashboard empty state (FR-007, SC-003)

1. When the library is empty AND no saved questions exist, a single whole-dashboard
   empty state is shown with an "Open the library" call to action.
2. Activating the action opens the library.
3. Whenever any documents or any saved questions exist, the empty state is not shown.

**Prohibited**: a page of zeros and blank panels for a first-time user; an empty state
with no call to action; the empty state coexisting with metric/panel content.

## U5 — Identity, accessibility, and theme (FR-009, FR-010, FR-011, SC-005, SC-006)

1. All 11 frozen Dashboard automation IDs remain present with unchanged navigation and
   meaning:

   `DashboardProfileCard`, `DashboardRefreshButton`, `DashboardMetricDocuments`,
   `DashboardMetricQueries`, `DashboardLibraryButton`, `DashboardAskButton`,
   `DashboardStatusBar`, `DashboardRetryButton`, `DashboardTileHistory`,
   `DashboardTileMyDocs`, `DashboardTileAdmin`.

2. Four stable new automation IDs identify the new metric cards: `DashboardMetricReady`,
   `DashboardMetricIndexing`, `DashboardMetricFailed`, `DashboardMetricMine`.
3. Every card and control is keyboard reachable in logical order with visible focus and
   a per-control accessible name; interactive targets are at least 44 DIPs.
4. Light, Dark, and High Contrast keep all counts, labels, focus, and status legible
   using theme tokens only — no colour literals, no `Opacity=`, no ad-hoc `FontSize`
   on text controls, only valid icon/theme keys and the shared text styles.
5. Existing handlers, navigation targets, refresh/retry behavior, and the shared
   behaviour layer are unchanged; no API, contract, storage, or retrieval change exists.

**Prohibited**: renaming or removing a frozen ID; changing a card's destination;
dropping a tooltip or accessible name; a hard-coded colour or dimming treatment.
