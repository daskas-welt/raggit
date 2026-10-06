# Data Model: Client UX Optimization

**Feature**: `029-client-ux-optimization` | **Date**: 2026-10-05 | **Spec**: [spec.md](spec.md)

No persisted entity, table, DTO, or API shape changes (FR-017). Everything below is transient
presentation state that lives in memory for the session. Field names are the planned ViewModel
surface; they describe behavior, not a storage schema.

## Search State (per surface)

Split across two homes, because the ViewModels and their pages are both transient and recreated on
every navigation (`App.xaml.cs`, `WpfNavigationService`):

- **`SearchSessionState`** — a DI singleton in the behavior layer, one string per surface (Library,
  History, My Documents, People directory). It survives navigation and dies on app restart, which is
  the session boundary FR-003 requires. Nothing is written to preferences or disk. This mirrors the
  existing `AskNavigationState` seam, which exists for the same reason.
- **The ViewModel** — holds the derived view computed from the loaded records and the current text.
  On construction it seeds `SearchText` from `SearchSessionState`; on change it writes back.

| Field | Type | Meaning |
|-------|------|---------|
| `SearchText` | string | What the user typed. Empty means no search is active. Leading/trailing whitespace is ignored for matching but preserved in the box. |
| `MatchCount` | int (derived) | Number of loaded records whose primary text contains `SearchText`, case-insensitively. Equals the unfiltered total when `SearchText` is empty. |
| `HasMatch` | bool (derived) | `MatchCount > 0`. Drives whether the no-match empty state replaces the list. |
| `IsSearchActive` | bool (derived) | `SearchText` is non-empty after trimming. Drives the match-count caption and the clear action. |

**Matching rule**: case-insensitive substring over the record's primary text — `Document.Filename`
(Library), `HistoryItem.PromptPreview` and `AnswerPreview` (History), `DocumentMineItem.Filename`
(My Documents). Punctuation, accents, and long strings match literally; nothing is an error.

**Relationship to existing state**:

- The Library's pager (`LibraryPage.Create`) receives the filtered list instead of the full list.
  Page count, status text, and the visible slice all derive from the filtered set (FR-004), and
  changing `SearchText` resets the page to 1.
- History and My Documents filter their loaded `Items`. `HasMore` and `LoadMore` are untouched;
  after new records append, the same filter re-applies. `MatchCount` therefore reflects loaded
  matches, not the server total — the match caption must say so (see contracts U1).
- `IsEmpty` (the genuine no-records state) and `HasMatch` (the no-match state) are distinct. A
  search can be active against an empty library; the no-records empty state wins, because clearing
  the search would not help.

## Attention State (Dashboard)

Derived entirely from counts `DashboardViewModel` already loads. No new fields are required; the
page computes the presentation from existing bindings.

| Condition | Source | Presentation |
|-----------|--------|--------------|
| Needs attention | `FailedDocuments > 0` | Error glyph (`ErrorCircle24`), critical tone, "Needs attention" caption |
| All clear | `FailedDocuments == 0` | Neutral glyph (`CheckmarkCircle24`), no caption |
| Unknown | `HasLoaded == false` | Em-dash placeholder, no attention styling |

**Rule**: attention styling renders only when `HasLoaded` is true, so a load failure can never show
an urgency signal over the error state (FR-007). The Indexing count is intentionally not an
attention condition — indexing is progress, not a problem.

## Column Priority (presentation rule, not stored)

Which columns survive at content widths of 720 DIPs or below. Applied by the existing page-owned
`SizeChanged` switch; sort order and sort indicators are independent of visibility and survive
collapse and restore.

| Surface | Always shown (primary) | Collapses at ≤720 DIPs | Where collapsed info survives |
|---------|------------------------|------------------------|-------------------------------|
| Library | Filename, Status, Download, Delete | Type, Size, Creator, Created | Restored on widening |
| People directory | Person (name + username) | Role, Account status, Action | The details panel shows role, status, lockout, and the account actions |

## Highlighted Match (People directory)

Not a new field. While the people search has text, the first item of the filtered view becomes
`SelectedUser` automatically, and the existing details panel — which already binds to
`SelectedUser` — shows that person's account actions. The administrator moves the highlight to act
on a different match. Clearing the search leaves the selection as-is.

**Ordering constraint**: auto-highlight runs after the existing rule that deselects a person
filtered out of view, so a stale selection never survives a keystroke.

## State Transitions

```text
SearchText changed
  → recompute filtered view synchronously (no debounce, no background work)
  → MatchCount / HasMatch update
  → Library: page resets to 1, pager re-slices the filtered list
  → History / My Documents: filtered view re-derives over loaded items
  → People directory: first filtered item becomes SelectedUser; panel follows

Load / Refresh completes
  → records replaced or appended
  → active SearchText re-applied to the new data (no flash of unfiltered content)
  → MatchCount recomputed

SearchText cleared
  → filtered view == full loaded list
  → no-match empty state hides
  → Library: page position restored as closely as the data allows
  → People directory: selection unchanged

Load fails
  → ErrorMessage set, shared status footer with retry (existing)
  → prior filtered view retained; no false "no matches" state

Dashboard load completes
  → HasLoaded = true → placeholders replaced by values, attention state evaluated
Dashboard load fails
  → HasLoaded stays false → placeholders cleared, error shown, no attention styling
```

## Validation Rules

- `SearchText` accepts any string including empty; no maximum length is enforced (a very long
  string simply matches nothing).
- Filtering is synchronous, so the displayed results always correspond to the current `SearchText`;
  there is no in-flight computation that could resolve out of order (spec edge case).
- `MatchCount` must equal the number of records actually shown (SC-002). For the Library that is
  the filtered total; for History and My Documents it is the filtered count of loaded records, and
  the caption distinguishes the two.
- Placeholder content never contains a numeral (spec Assumption: no fabricated numbers).
