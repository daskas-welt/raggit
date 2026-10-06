# Phase 0 Research: Client UX Optimization

**Feature**: `029-client-ux-optimization` | **Date**: 2026-10-05 | **Spec**: [spec.md](spec.md)

All Technical Context items were resolvable from the codebase and the component library's source.
No `NEEDS CLARIFICATION` survives. Each decision below names the spec requirement it serves.

## R1 — Search is client-side filtering, but the three surfaces differ in what they have loaded

**Decision**: Add a `SearchText` property and a derived filtered view to each of
`LibraryViewModel`, `HistoryViewModel`, and `DocumentsMineViewModel`. Matching is
case-insensitive substring (`IndexOf` with `OrdinalIgnoreCase`) over the record's primary text:
`Document.Filename` for the Library, `HistoryItem.PromptPreview` + `AnswerPreview` for History,
`DocumentMineItem.Filename` for My Documents. Filtering runs synchronously on the UI thread when
`SearchText` changes.

**Rationale**: The spec (Assumptions, FR-017) forbids new endpoints and workstation-side search, so
filtering must happen over records the client already holds. Verified load shapes:

- **Library** fetches the full list once (`DocumentsApiClient.GetDocumentsAsync`, no paging
  parameters) and pages it in memory (`LibraryPage.Create`). Search therefore sees everything, and
  the existing pager already slices an arbitrary in-memory list — the filtered list simply becomes
  its input (FR-004).
- **History** and **My Documents** load incrementally (`GetHistoryAsync(limit, offset)`, page size
  20, `LoadMore` appends). Search can only see loaded records. The spec explicitly accepts this
  ("a surface that loads incrementally searches what is loaded and its existing load-more extends
  the searchable set"), so no fetch-all behavior is added.

**The spec's own tension, resolved**: Story 1's independent test ("locate a specific past question
using only the new search — no paging or scrolling") cannot hold for a question beyond the first
loaded page without either server search (forbidden) or auto-loading everything (contradicts the
incremental-load assumption). Resolution: the test's "no paging or scrolling" bar applies to the
Library unconditionally and to History/My Documents **within the loaded set**; reaching an unloaded
record still uses the existing "Load more", after which the search re-applies (the edge case "the
search reapplies to freshly loaded data" already requires this). Stated in the plan's Summary so
acceptance testing uses the right bar per surface.

**Alternatives considered**: Server-side search — rejected, violates FR-017 and the spec's
Assumptions. Auto-fetching all pages when a search starts — rejected, changes request volume and
contradicts the incremental-load assumption. `ICollectionView` filtering — viable, but the
ViewModels currently expose plain collections and derive paging themselves; a plain computed
filtered list keeps one code path and stays unit-testable without a `Dispatcher`.

## R2 — The tenth-of-a-second bound needs no machinery

**Decision**: No debounce, no background filtering, no virtualization work. Filtering is an
in-memory scan over the loaded collection, raised through the existing
`ObservableProperty`/`OnPropertyChanged` path.

**Rationale**: SC-002 requires results within 100 ms of the text changing. A linear scan of the
volumes this client holds (hundreds to low thousands of records) completes in well under a
millisecond, orders of magnitude inside the bound. Debouncing would *delay* results and make the
bound harder to meet, so it is rejected. The edge case "results correspond to the latest text,
never an earlier keystroke" holds automatically because filtering is synchronous — there is no
stale in-flight computation to discard.

**Verification**: a unit test asserting the filtered view matches the text immediately after
`SearchText` changes (deterministic, no timing). The 100 ms figure is a UX ceiling validated by the
`winapp ui` walkthrough, not a unit-test assertion — a timing assertion would be flaky on a loaded
CI agent and would not catch a real regression that a synchronous implementation can produce.

**Alternatives considered**: Debounced filtering (250 ms) — rejected, spends most of the latency
budget on nothing and complicates the "latest text wins" edge case. A stopwatch-based unit test —
rejected as flaky; the implementation strategy makes the bound structurally true.

## R3 — Search text needs a session-scoped home, because the ViewModels are transient

**Decision**: A new `SearchSessionState` singleton in `RAGGit.Client.Core` holds the search text for
the Library, History, and My Documents surfaces (and the People directory, which has the same
lifetime). Each ViewModel reads its text from the state on construction and writes back when it
changes. The derived filtered view stays on the ViewModel, computed from loaded data.

**Rationale**: FR-003 requires search text to survive refresh and leaving/returning to the surface
for the session. Verified registrations (`App.xaml.cs:225-229`): `LibraryViewModel`,
`HistoryViewModel`, and `DocumentsMineViewModel` are `AddTransient`, and so are their pages
(`App.xaml.cs:241-245`). WPF-UI navigation creates a fresh page per visit
(`WpfNavigationService` navigates by type), so a property on the ViewModel is discarded on every
navigation — it cannot satisfy FR-003. The established fix already exists in the codebase:
`AskNavigationState` is a DI singleton precisely because "pages are transient, so a preset on a
freshly resolved ViewModel would be discarded — the singleton survives navigation instead"
(`AskNavigationState.cs:6-10`). `SearchSessionState` follows that pattern, holding one string per
surface instead of a read-once value. It dies on app restart, which is the session boundary, so
nothing is persisted (no preferences, no disk).

**Alternatives considered**: Registering the three ViewModels as singletons — rejected, it would
retain loaded document and history collections for the process lifetime and change behavior beyond
this feature. Persisting search text to `ILibraryPreferences` — rejected, exceeds session scope and
adds storage the spec forbids. Accepting the loss on navigation — rejected, it fails FR-003
outright.

## R4 — People-directory headers: pin them, do not replace the control

**Decision**: Keep the Admin `ListView` + `GridView` and pin its header visually: suppress the
`GridView` header row and render an identical header row as a sibling above the list, reusing the
existing `GridViewColumnHeader.Click` sort handler. Column widths stay shared so header and cells
cannot drift.

**Rationale**: WPF-UI 4.3.0 ships no `DataGrid` and no table control with a frozen header (verified
against the library's control set and docs). The current People directory uses `ui:ListView` with a
`GridView`, whose header row is part of the scrollable content — the defect FR-008 names. A pinned
header built from the same column definitions preserves the existing sort behavior, virtualization,
and the per-row inline editing (role `ComboBox`, enable/disable button) that a control swap would
put at risk. The Library already proves the pattern: its header row is a separate `Grid` above the
`ListView`, kept aligned by shared column resources (`LibraryPage.xaml:110-179`).

**Alternatives considered**: Swapping in the stock WPF `DataGrid` — rejected twice over: the repo
standard is Fluent `ui:*` controls only (AGENTS.md), and `DataGrid` would replace working inline
editing and the frozen automation IDs for a cosmetic gain. A third-party frozen-header control —
rejected, no new dependencies for a presentation fix.

## R5 — Narrow-width column collapse extends the existing mechanism

**Decision**: Extend the page-owned `SizeChanged` switch (threshold 720 DIPs, already used by
Library, Dashboard, and Admin) to collapse one more tier of columns. Primary content — name,
status, row actions — is never collapsed.

- **Library**: additionally collapse Type (70) and Size (90) below 720. Creator and Created already
  collapse. Remaining: Filename, Status, Download, Delete — fits the minimum window with the
  navigation pane open (FR-009, SC-005).
- **People directory**: collapse Role (135), Account status (190), and Action (132) below 720,
  keeping Person (235). The collapsed information stays reachable because the details panel already
  shows role, status, lockout, and the account actions (FR-010's "still reachable" clause, verified
  in `AdminUsersPage.xaml:510-585`).

**Rationale**: This is the precedent the spec's Assumptions point at ("follow the precedent already
set for the Library's secondary columns"). Reusing the resource-key mechanism means header row and
realized row templates collapse and restore together, including rows realized after the switch
(`DynamicResource` propagation — the comment in `LibraryPage.xaml:22-30` documents why). Sort state
is untouched by visibility, satisfying the "sort persists silently" edge case.

**Deferred item settled here**: the spec left "which exact columns collapse" to planning. The rule
applied: a column collapses when the row's primary content (name, status, actions) is unusable
without it collapsing, and only when its information survives elsewhere. Role/status/action in the
People directory pass that test; the Person column does not, so it stays.

**Alternatives considered**: Horizontal scrolling as the small-window answer — rejected, it is the
defect SC-005 measures. Collapsing the People directory's action column without a fallback —
rejected, FR-010 requires the information to remain reachable.

## R6 — Account actions from search: auto-highlight the first match

**Decision**: While `UserSearchBox` has text, the filtered view's first item becomes
`SelectedUser` automatically. The details panel already binds to `SelectedUser`
(`AdminUsersPage.xaml:483-588`), so role, status, reset-password, and the enable/disable path
appear with no new controls and no row click. The administrator moves the highlight with arrow keys
or a click to act on a different match. Clearing the search restores the prior selection behavior.

**Rationale**: The clarification (Session 2026-10-05, Q1) chose the details panel over per-row
buttons, and the panel is entirely selection-driven today. Auto-selecting the first filtered item
is the smallest change that removes the selection step (SC-007) while reusing every existing
confirmation flow unchanged (FR-011). Keyboard reach is free: the list is already focusable and its
selection already drives the panel (edge case "keyboard-only use").

**Caveat to honor**: the current filter logic deselects when the selected person is filtered out
(`AdminUsersPage.xaml.cs:121-123`). Auto-highlight must run *after* that, so a stale selection never
survives a keystroke.

**Alternatives considered**: Per-row action buttons in the result template — rejected by the
recorded clarification. A separate "quick actions" popup — rejected, a new surface duplicating the
panel.

## R7 — Dialog action order: one rule, and the shared confirm already complies

**Decision**: Standardize on **confirming action first, dismissing action last**.

- **Shared confirm** (`WpfDialogService.ConfirmAsync`): already compliant. Verified against the
  WPF-UI 4.3.0 `ContentDialog` template — the footer lays out Primary, then Secondary, then Close
  left-to-right, so `PrimaryButtonText = "Yes"` / `CloseButtonText = "No"` renders the confirming
  action first and the dismissing action last. No change, and every caller (delete document, role
  change, enable/disable, sign-out) inherits compliance at once.
- **Create Person and Reset Password**: non-compliant today — `Cancel` is declared before the
  committing button (`CreatePersonDialog.xaml:63-89`, `ResetPasswordDialog.xaml:42-70`). Swap the
  declaration order. These commit data, so their confirming action is non-destructive.
- **Upload dialog**: footer is already Upload, Cancel, Close — compliant. Its in-window cancel
  confirmation has the destructive confirming action ("Yes, cancel") first and "Keep uploading"
  last — also compliant, and it is the template for the destructive visual treatment
  (`Appearance="Danger"`).

**Verified at implementation (T020)**: `WpfDialogService.ConfirmAsync` sets `PrimaryButtonText = "Yes"` and `CloseButtonText = "No"`, which the 4.3.0 template renders confirming-first. The Upload dialog footer is Upload, Cancel, Close, and its cancel confirmation places "Yes, cancel" (`Appearance="Danger"`) before "Keep uploading". Both already comply; only Create Person and Reset Password needed reordering.

**Destructive treatment**: `Appearance="Danger"` on a confirming action that destroys or abandons
work, `Appearance="Primary"` otherwise. The shared confirm stays non-danger (its "Yes" confirms
both destructive and benign operations today; restyling it per-caller would change a shared seam
for a cosmetic distinction the spec's dialogs already make locally). Recorded as a deliberate
boundary: FR-012's destructive distinction applies to the dialogs this feature touches, and the
shared confirm's single "Yes" is documented as the exception rather than silently half-fixed.

**Rationale**: The clarification (Q3) fixed the order but not the mechanism. Following the control's
built-in order means compliance is a property of the template, not per-dialog discipline, wherever
`ContentDialog` is used.

**Alternatives considered**: Reordering the `ContentDialog` template so Close leads — rejected,
forks a library template and fights the platform convention. Per-dialog bespoke ordering — rejected,
it is the inconsistency being removed.

## R8 — Dashboard attention and placeholders use what the page already has

**Decision**: Two presentation changes on `DashboardPage`, no new data.

- **Attention (FR-005)**: the Failed metric already swaps its glyph color at zero
  (`DashboardPage.xaml:345-364`). Extend that trigger to also swap the glyph (`ErrorCircle24` when
  non-zero, `CheckmarkCircle24` at zero) and add a short text cue ("Needs attention") so the
  distinction is carried by shape and text, not color — readable in high contrast (SC-003). Scope
  stays the failure count only; the Indexing metric is not an attention signal (settling the
  deferred item: indexing is progress, not a problem, and the spec's Assumption limits attention to
  conditions already computed as needing action).
- **Placeholders (FR-007)**: no skeleton or shimmer control exists in WPF-UI 4.3.0 (verified). Use
  the layout itself: render the metric cards with em-dash placeholders and secondary-text styling
  while `HasLoaded` is false, replaced by real values on success. Em dashes are visibly not numbers,
  satisfying "never display fabricated numbers." The existing centered ring remains for the failure
  path; attention styling is gated on `HasLoaded` so it can never paint over an error (FR-007's last
  clause).
- **Destination cues (FR-006)**: the metric cards already carry destination tooltips and accessible
  names. Add a visible text cue (a caption naming the destination, e.g. "Library") so the
  destination is apparent without hovering — SC-004's gap is specifically "visible treatment."

**Rationale**: Every value involved is already on `DashboardViewModel`; this is styling of existing
bindings. Keeping it in the page avoids a behavior-layer change for a purely visual distinction.

**Alternatives considered**: A new `AttentionLevel` enum in Core — rejected, it would encode a
presentation rule in the behavior layer for one boolean condition. A shimmer animation — rejected,
no control supports it and an animation adds nothing the placeholder layout does not.

## R9 — Progress text is copy, bound to busy state that already exists

**Decision**: Add one plain-language line bound to the existing busy flag on each surface, hidden
when the flag clears.

- **Sign-in**: "Signing in…" beneath the sign-in button while `LoginViewModel.IsBusy`, removed on
  completion or failure (the error already renders through the shared status footer).
- **Answer preparation**: the conversation already shows "Preparing answer…"
  (`ChatControl.xaml:261-269`). Extend it to name the step rather than replace it, and keep it
  inside the conversation so prior messages stay readable (FR-013). No new busy state.

**Long answers (FR-014)**: `ChatControl.ScrollToNewest` already scrolls the newest item into view
(`ChatControl.xaml.cs:137-148`), which brings the *bottom* of a long answer into view. Change it so
the top of the newest message aligns with the top of the viewport, keeping the opening visible and
the rest scrollable. This is the one behavior change in the chat surface; auto-scroll itself stays.

**Rationale**: Both busy flags exist and already drive spinners. The spec asks for words alongside
them, which is a bound `TextBlock` with a visibility converter — no new state, no timing logic.

**Alternatives considered**: Staged progress ("embedding… searching… writing…") — rejected, the
client has no per-stage signal and inventing one implies API changes FR-017 forbids.

## R10 — What is deliberately not researched further

- **Localization**: out of scope per spec; all new copy is English, matching existing micro-copy.
- **Server contracts**: no `contracts/api.yaml` change. The UI contract document is the only
  contract artifact (Phase 1).
- **Performance beyond filtering**: the spec excludes load-path tuning; nothing here changes request
  volume except where R1 says otherwise (it does not).
