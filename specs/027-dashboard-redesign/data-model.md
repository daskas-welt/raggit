# Data Model: Dashboard Redesign — "Work overview"

**Feature**: `027-dashboard-redesign` | **Date**: 2026-10-01 | **Plan**: [plan.md](plan.md)

No persisted entities are added or changed. Everything below is transient view state
bound from the existing view model or derived from the current content width.

## Explicitly out of scope

No new table, field, DTO, API shape, or stored preference. Upload behavior, navigation
targets, and the load/refresh behavior of the existing view model are unchanged.

## Entity: Metric values (transient, existing)

Bound read-only from the already-available dashboard numbers; the page introduces no new
data source.

| Card | Existing value | Destination |
|------|---------------|-------------|
| Documents | total document count | Library |
| Ready | ready document count | Library |
| Indexing | indexing document count (uploading/queued/indexing) | Library |
| Failed | failed document count | Library |
| Questions | saved-question count | Ask |
| Mine | personal document count | My Docs |

Zero is a displayable value, not an absence: a zero card still renders and still
navigates.

## Entity: Panel content (transient, existing)

| Panel | Content | Empty condition |
|-------|---------|-----------------|
| Recent documents | recent document list (identity, state, time) | total document count is zero |
| Recent questions | recent question list (preview, citations) | saved-question count is zero |

## Entity: Whole-dashboard empty state (transient, derived)

Shown if and only if the library is empty AND no saved questions exist. Carries the
"Open the library" call to action. Hidden whenever either count is non-zero; it never
coexists with metric/panel content.

## Entity: Layout mode (transient, derived)

Owned by the page, not persisted and never sent anywhere.

| Field | Values / rule |
|-------|---------------|
| Layout mode | Wide (panels side by side) or compact stacked, derived from page content width |
| Breakpoint | Compact stacked when content width is at most 720 DIPs |

## Entity: Refresh outcome (transient, existing behavior)

| Field | Rule (agreed, view model unchanged) |
|-------|--------------------------------------|
| Last-known-good | On failed refresh, previously loaded numbers stay visible next to the error status |
| First-load failure | Error status shows; no numbers are presented as loaded data |

## State transitions

- Load succeeds → six cards, both panels (or their empty states), and status update
  together; whole-dashboard empty state shows only when both counts are zero.
- Load fails after a success → error status appears; last-known numbers remain.
- Content width crosses 720 DIPs → panels switch between side-by-side and stacked;
  header, cards, and status are unaffected.
- Role changes (admin vs non-admin) → Admin quick action appears/disappears on next
  load; all other header content is unaffected.
