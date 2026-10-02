# Data Model: Dashboard Redesign — "Work overview"

**Feature**: `027-dashboard-redesign` | **Date**: 2026-10-01 | **Plan**: [plan.md](plan.md)

No persisted entities are added or changed. Everything below is transient view state
bound from the view model or derived from the current content width.

## Explicitly out of scope

No new table, field, DTO, API shape, or stored preference. Upload behavior and navigation
targets are unchanged. The view model's load/refresh behavior is unchanged except for two
post-review signals documented under Refresh outcome: StatusSeverity no longer resets when a
load starts, and a new HasLoaded flag distinguishes a confirmed dashboard from an unloaded
one.

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

## Entity: Refresh outcome (transient)

| Field | Rule |
|-------|------|
| HasLoaded | True once a load has completed successfully; never resets. The library summary and the six metric cards stay hidden while it is false, so an unloaded dashboard never presents its initial zeros as loaded data |
| Last-known-good | On failed refresh, previously loaded numbers stay visible next to the error status; HasLoaded stays true, so the cards keep showing them |
| First-load failure | Error status shows; HasLoaded stays false, so the library summary and metric cards stay hidden — no numbers are presented as loaded data |
| Status severity | Carries the outcome of the last completed load and is not reset when a load starts, so the whole-dashboard empty state does not flicker off while a refresh is in flight |

## State transitions

- First load succeeds → HasLoaded turns true; the library summary and the six cards appear
  with the loaded values; both panels (or their empty states) and the status update
  together; whole-dashboard empty state shows only when both counts are zero.
- First load fails → error status shows; the summary and cards stay hidden (HasLoaded
  false); retry is offered.
- Load fails after a success → error status appears; HasLoaded stays true, so the
  last-known numbers and summary remain visible next to the error status.
- Refresh is in flight → the empty state keeps the last confirmed outcome (StatusSeverity
  is not reset), so an empty library's empty state does not flicker off mid-refresh.
- Content width crosses 720 DIPs → panels switch between side-by-side and stacked;
  header, cards, and status are unaffected.
- Role changes (admin vs non-admin) → Admin quick action appears/disappears on next
  load; all other header content is unaffected.
