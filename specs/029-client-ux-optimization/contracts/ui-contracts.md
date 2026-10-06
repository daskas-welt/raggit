# UI Contracts: Client UX Optimization (U1–U6)

**Feature**: `029-client-ux-optimization` | **Date**: 2026-10-05 | **Spec**: [spec.md](spec.md)

Behavioral contracts for the surfaces this feature changes. Each clause is observable without
reading implementation. These extend the feature-028 contracts; where a 028 contract already governs
a surface (shared header, status footer, busy idiom, automation-ID freeze), it still governs and is
not restated here.

No `contracts/api.yaml`: this feature changes no endpoint, request, or response (FR-017).

## U1 — Record search (FR-001, FR-002, FR-003, FR-004, SC-001, SC-002)

Applies to the Library, History, and My Documents surfaces.

1. Each surface shows one search box above its records, with placeholder text naming what it
   searches ("Search documents" / "Search questions" / "Search my documents"), and a clear action
   that is visible only while the box has text.
2. Results narrow as the user types, case-insensitively, over filename (Library, My Documents) or
   question and answer text (History). The displayed results always correspond to the current text —
   never to an earlier keystroke.
3. Results reflect the typed text within a tenth of a second (SC-002). This bound holds because
   filtering runs over records already loaded; it is validated by walkthrough, not by a timed test.
4. While a search is active, a caption states the match count and the clear action is shown. The
   Library states the filtered total ("3 of 120 documents"). History and My Documents state the
   loaded match count ("3 matching of 20 loaded"), because they only hold one page at a time and
   load more on demand — the caption must not imply a server-wide total.
5. A search matching nothing shows a no-match empty state naming the search and offering the clear
   action. It is distinct from the no-records empty state, which wins when the surface has no
   records at all.
6. Paging operates over the filtered set: the Library's page count and "Showing X–Y of Z" reflect
   filtered totals, and changing the search text returns to the first page. History and My Documents
   keep their existing "Load more", which extends the searchable set; the search re-applies when new
   records arrive.
7. Search text survives refresh and leaving and returning to the surface, and is gone after the app
   restarts. Clearing it restores the full loaded list.
8. A load failure shows the shared error status with retry and never a false no-match state.

**Prohibited**: a search that queries the workstation, a debounce the user can perceive, a match
count that disagrees with the records shown.

## U2 — Dashboard attention and wayfinding (FR-005, FR-006, FR-007, SC-003, SC-004)

1. The Failed metric is visually distinct exactly when its count is greater than zero: an error
   glyph, critical tone, and the caption "Needs attention". At zero it shows a neutral glyph and no
   caption. The distinction is carried by glyph shape and text, not color alone, so it survives high
   contrast.
2. No other metric shows attention styling. The Indexing metric is progress, not a problem.
3. Attention styling appears only after a successful load. During loading and after a load failure,
   no metric shows urgency.
4. While the first load is in flight, the metric layout renders with em-dash placeholders in place
   of numbers — visibly not data — and the placeholders are replaced by real values when the load
   completes.
5. Every metric card shows, in its visible content, the page it opens (a caption such as "Library",
   "Ask", or "My documents"), in addition to the tooltip and accessible name it already carries.

**Prohibited**: a placeholder containing a digit; attention styling rendered over an error state; a
metric card whose destination is discoverable only by hovering.

## U3 — Dense tables at narrow widths (FR-008, FR-009, FR-010, SC-005, SC-006)

1. The People directory's column headers stay visible while its rows scroll, and the header shows
   which column is sorted and in which direction, at any scroll position.
2. At content widths of 720 DIPs and below, tables reduce to their primary content so no horizontal
   scrolling is needed to reach it:
   - Library keeps Filename, Status, and the row actions; Type, Size, Creator, and Created collapse.
   - People directory keeps the Person column; Role, Account status, and Action collapse.
3. Widening past the threshold restores every collapsed column in its previous order, with sort
   order and sort indicator unchanged.
4. Information from a collapsed column stays reachable without widening: the People directory's
   details panel shows role, status, lockout, and the account actions for the selected person.
5. Collapsing never changes which row is selected, the sort order, or the loaded data.

**Prohibited**: a horizontal scrollbar required to reach a row's name, status, or actions at the
minimum supported window size with the navigation pane open; a sort indicator that lies about the
order after columns collapse and restore.

## U4 — Account actions from search (FR-011, SC-007)

1. While the People directory search has text, the first matching person is highlighted
   automatically and the details panel shows that person's actions — reset password, change role,
   enable/disable — with no row click.
2. Moving the highlight (click or arrow keys) updates the panel to that person. Keyboard alone is
   sufficient to reach and activate every action.
3. Activating an action opens the same confirmation used by the directory's existing controls, and
   the outcome is reflected in both the list and the panel. The search text is unchanged by the
   action.
4. Clearing the search leaves the highlighted person selected.
5. If the highlighted person was removed by another session, the action's existing error
   presentation explains it and the row disappears on the next refresh.

**Prohibited**: a second, separate set of account actions that can drift from the directory's own;
any account operation that does not already exist in the directory.

## U5 — Dialog action order (FR-012, SC-008)

1. In every confirmation and task dialog, the confirming action comes first and the dismissing
   action (Cancel, Close, or Keep) is the last button.
2. A confirming action that destroys or abandons work uses the danger treatment; every other
   confirming action uses the primary treatment. The same meaning maps to the same treatment in
   every dialog.
3. The shared Yes/No confirmation already follows clause 1, and all of its callers (delete document,
   role change, enable/disable, sign-out) inherit that compliance. Its "Yes" is not restyled
   per-caller: one shared confirm serves both destructive and benign questions, and the distinction
   in clause 2 is carried by the dialogs that own their own buttons.
4. Create Person and Reset Password place their committing action first and Cancel last. The Upload
   dialog already complies, including its cancel-mid-upload confirmation, which is the reference for
   the danger treatment.

**Prohibited**: a dialog whose final button commits; a destructive action styled as primary, or a
non-destructive one styled as danger, in any dialog this feature touches.

## U6 — Progress text and long answers (FR-013, FR-014, SC-009)

1. While sign-in is in progress, the Login page shows the plain-language line "Signing in…" in
   addition to the button's busy indicator. The line is gone when sign-in completes or fails, and it
   never appears together with the error status.
2. While an answer is being prepared, the conversation shows a plain-language progress line naming
   what is happening, the conversation so far stays visible, and the line is removed when the answer
   arrives or the request fails.
3. When an answer arrives, its opening is visible without manual scrolling and the remainder is
   reachable by scrolling down. The newest message never lands with only its tail in view.

**Prohibited**: a progress line that survives the operation it describes; staged progress text
implying steps the client cannot actually observe.

## Automation identifiers

1. Preserved unchanged: every identifier frozen by features 022, 023, and 028, including
   `UserSearchBox`, `UsersList`, `SelectedPersonDetails`, `OpenResetPasswordButton`,
   `DashboardMetric{Documents,Ready,Indexing,Failed,Queries,Mine}`, `SignInButton`, and
   `ChatInputBox`.
2. New stable identifiers:
   - `LibrarySearchBox`, `LibrarySearchClearButton`, `LibraryMatchCount`, `LibraryNoMatchState`
   - `HistorySearchBox`, `HistorySearchClearButton`, `HistoryMatchCount`, `HistoryNoMatchState`
   - `DocumentsMineSearchBox`, `DocumentsMineSearchClearButton`, `DocumentsMineMatchCount`,
     `DocumentsMineNoMatchState`
   - `DashboardMetricFailedAttention` (the attention caption, present only when non-zero)
   - `LoginProgressText`

## Design-system compliance

All new elements use the established scale: `PagePadding` and the 4px spacing rhythm for layout,
`SecondaryText` for captions and placeholders, `EmptyStateGlyph` for the no-match state, and theme
tokens exclusively for color. No new ad-hoc spacing, sizing, or color values (FR-016). New glyphs
come from the existing vocabulary: `Search24` for search, `Dismiss24` for clear, `ErrorCircle24`
and `CheckmarkCircle24` for attention.
