# Quickstart: Validating Client UX Optimization

**Feature**: `029-client-ux-optimization` | **Date**: 2026-10-05 | **Spec**: [spec.md](spec.md)

Runnable checks that prove the feature end to end. Contracts are in
[contracts/ui-contracts.md](contracts/ui-contracts.md); state semantics are in
[data-model.md](data-model.md). This is a validation guide — it does not contain implementation.

## Prerequisites

- .NET SDK 10.0.401 (pinned by `global.json`), Windows 10 1809+ or Windows 11.
- A built client pointed at a running workstation API (see the repo root `AGENTS.md` for the API
  port, the operator CLI that creates a local login, and the `Workstation:ApiKey` gotcha).
- Two accounts: one Admin, one Employee.
- Fixture data: at least 60 documents (so the Library pages and a search crosses a page boundary),
  at least 25 saved questions for the signed-in person (so History has a "Load more"), and at least
  one document in the `Failed` status.

## 1. Build and regression gate

```text
dotnet build RAGGit.sln -c Release -p:Platform=x64
dotnet test RAGGit.sln -c Release --no-build
dotnet csharpier check .
```

Expected: build succeeds, all existing suites pass with zero assertion changes (SC-010), formatting
is clean. The new unit tests from step 2 are part of this run and must be green.

## 2. Unit tests (test-first, behavior layer)

Written before the implementation, run via `dotnet test` filtered to the new fixtures. Each encodes
a clause from [data-model.md](data-model.md):

- **Search filtering**: setting `SearchText` narrows `LibraryViewModel` results by filename,
  case-insensitively; punctuation matches literally; empty text returns everything; `MatchCount`
  equals the filtered count.
- **Paging over the filtered set**: with 60 documents and a page size of 20, a search matching 25
  yields 2 pages, and changing `SearchText` returns to page 1.
- **History and My Documents**: filtering applies to loaded items only; after `LoadMore`, the filter
  re-applies and `MatchCount` counts loaded matches, not the server total.
- **Refresh persistence**: replacing the loaded collection re-applies the current `SearchText`; no
  unfiltered state is observable.
- **People directory**: with search text set, the highlighted person is the first match; clearing
  the search does not change the highlight; a keystroke never leaves a filtered-out person
  highlighted.

## 3. Static audit

Extend the design audits in `specs/023-design-system-refinement/quickstart.md` with:

- No new hard-coded color, no `Opacity=` emphasis, no ad-hoc spacing values outside the 4px scale on
  the touched surfaces (U2, U6, design-system compliance).
- Every new interactive element carries its automation identifier from the contracts, and no
  pre-existing identifier is renamed or removed.
- Dialog button order: in `CreatePersonDialog.xaml` and `ResetPasswordDialog.xaml` the committing
  button is declared before Cancel (U5).

## 4. Walkthrough (`winapp ui`)

Drive the client against the live API. Each row maps to a contract clause.

| # | Do | Expect |
|---|----|--------|
| 1 | Library: type part of a known filename | Only matching rows remain, within a beat; caption reads "N of M documents" (U1.2–U1.4) |
| 2 | Type text matching nothing | No-match empty state with a clear action; activating it restores the full list (U1.5, U1.7) |
| 3 | Search matching 25+ documents, then page forward | Page count and "Showing X–Y of Z" reflect the filtered total (U1.6) |
| 4 | Refresh, leave the page, return | Search text and results are unchanged (U1.7) |
| 5 | History: search, then "Load more" | New records are filtered too; caption says "matching of N loaded", never a server total (U1.4, U1.6) |
| 6 | Repeat 1–2 on My Documents | Same behavior and the same presentation (U1.1) |
| 7 | Dashboard with a failed document | Failed metric shows the error glyph and "Needs attention" (U2.1) |
| 8 | Dashboard with zero failures | Failed metric shows the neutral glyph and no caption (U2.1) |
| 9 | Sign in and watch the Dashboard arrive | Em-dash placeholders, then real values; no digit appears before the load completes (U2.4) |
| 10 | Stop the API, open the Dashboard | Error status with retry; no metric shows urgency (U2.3) |
| 11 | Read each metric card | Each names its destination in visible text (U2.5) |
| 12 | People directory, 100+ people: scroll to the bottom | Column headers and the sort indicator stay visible (U3.1) |
| 13 | Resize to 800×600 with the nav pane open, on Library and People | No horizontal scrollbar to reach name, status, and actions (U3.2) |
| 14 | Widen again | Collapsed columns return; sort order unchanged (U3.3) |
| 15 | At 800×600, select a person | Details panel still shows role, status, and account actions (U3.4) |
| 16 | People search: type a name | First match is highlighted and the panel shows their actions without any row click (U4.1) |
| 17 | Reset that person's password from the panel | The same confirmation as before; search text survives; list and panel reflect the outcome (U4.3) |
| 18 | Arrow down to the next match, disable the account | Panel followed the highlight; keyboard alone was sufficient (U4.2) |
| 19 | Open delete-document, sign-out, Create Person, Reset Password, and the Upload cancel confirmation | Confirming action first, dismissing action last in every one; danger styling only on the destructive confirming actions (U5.1, U5.2) |
| 20 | Sign in against a slow workstation | "Signing in…" shows during the wait and is gone after; it never overlaps the error (U6.1) |
| 21 | Ask a question with a long answer | Progress line shows, prior messages stay readable, and the answer arrives with its opening in view (U6.2, U6.3) |
| 22 | Switch to high contrast and repeat 7 and 13 | Attention distinction and collapsed tables remain legible (FR-016) |

## 5. Done when

- Steps 1–3 are green.
- All 22 walkthrough rows match their expected column, with screenshots captured alongside the
  earlier features' evidence.
- `SC-010` holds: no pre-existing automation identifier changed, and no existing test assertion was
  edited.
