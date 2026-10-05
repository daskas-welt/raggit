# Walkthrough evidence — 028-pages-ux-polish (T042/T043)

**Date**: 2026-10-02 | **Walkthrough**: quickstart.md parts 1–8 | **Contracts**: U1–U8
**Verdict**: all 9 expected-outcomes rows PASS (two with minor follow-ups noted below).

## Environment

- **API**: `src/RAGGit.Workstation.Api\bin\Release\net10.0\RAGGit.Workstation.Api.exe`,
  run from `src/RAGGit.Workstation.Api` (CWD-relative SQLite path),
  `--urls https://localhost:5001;http://localhost:5142`.
  Env keys `Api__AdminKey=walk-admin-key`, `Api__EmployeeKey=walk-employee-key`.
- **Client**: `src/RAGGit.Client.WPF\bin\Release\net10.0-windows10.0.17763.0\RAGGit.Client.WPF.exe`.
  Only the **output** `appsettings.json` was patched (added `Workstation:ApiKey:
  walk-admin-key`); the source file is untouched.
- **Accounts** (operator CLI): `walker` / Admin / `Passw0rd!`, `viewer` / Employee /
  `Passw0rd!`. Dev HTTPS cert trusted; Ollama up (`qwen2.5:3b`,
  `snowflake-arctic-embed2`).
- **Seed data**: `raggit-overview.txt` + `ux-polish-notes.txt` (both Ready), one
  saved cited Q&A asked as walker via REST. Filler uploads created during the
  run were deleted afterwards; the library ends **empty**.
- **Driver**: `winapp ui` against client PIDs 17660 / 21904. Screenshots use
  `--capture-screen` (plain capture intermittently returns black/stale frames —
  the 023 "capture pitfall"); repaint forced by navigating away and back.
- Screenshots below are relative to `specs/028-pages-ux-polish/evidence/`.

## Part 1 — Header parity (U1) — PASS

Every content page opens with the same header-card structure (title +
one-line supporting text left, actions right):

| Page | Header | Evidence |
|------|--------|----------|
| Dashboard | "Dashboard / Your library at a glance" + History/My Docs/Admin/refresh; profile card on the post-header row (library summary left, profile right) | `04-dashboard-after-login.png` |
| Library | "Document Library / Documents available to everyone" + refresh/upload | `05-library.png` |
| Ask | "Ask the Library / Find grounded answers with citations" + Clear | `06-ask.png` |
| QueryDetail | "Query Detail / A saved question and its grounded answer" + Ask again/Back; Prompt/Answer/Citations section labels | `12-querydetail.png` |
| History | "Query History / Your past questions" + refresh | `07-history.png` |
| My Documents | "My Documents / Documents you uploaded" + refresh | `16-mydocs.png`, `54-mydocs-walker.png` |
| Admin | "People Management / Manage accounts and access" + Add person/refresh | `17-admin.png` |
| Settings | "Settings / Preferences for this client" | `21-settings.png` |

"Did you mean?" suggestion header is single-language (`QueryPage.xaml:69`);
chips render only with person suggestions (none produced in this run — verified
statically).

## Part 2 — Status + retry (U2) — PASS

API stopped → refresh/navigation on each page shows the shared
InfoBar-based `StatusFooterControl` (severity + wrapped message + retry),
content stays visible:

- Library: `31-library-load-failed.png` (long "cannot reach AI workstation…"
  error wraps fully; table intact).
- Dashboard: `34-dashboard-load-failed.png` (`DashboardRetryButton` present).
- History / My Documents / Admin: `HistoryRetryButton`,
  `DocumentsMineRetryButton`, `AdminRetryButton` all present on failed loads;
  Admin failure captured in `35-admin-load-failed.png`.
- Login: Enter-submit with API down → `LoginStatusMessage` shows the shared
  wrapped "cannot reach…" error (`62-login-unreachable.png`); the Sign in
  button itself is the retry affordance (no separate retry on Login by design).
- API restarted → Admin retry recovers to "Loaded 6 users."
  (`36-admin-retry-ok.png`); Dashboard (`37-dashboard-recovered.png`) and
  History (`38-history-recovered.png`, 2 saved queries) reload on visit.
- No native message box anywhere: single OS window throughout
  (`list-windows` checks after every dialog/confirm).

## Part 3 — Busy (U3) — PASS

- Ask send: user bubble + send-button→ring morph + "Preparing answer…"
  status row with input bar; conversation visible, no overlay
  (`09-ask-inflight.png`).
- Download row: button morphs to ring while in flight, other rows stay
  actionable (`32-download-failed.png` — captured against the failure run,
  same morph path as success).
- Refresh re-renders with content visible (ring window <1 s on localhost;
  table never blanks — `30-refresh-busy.png` post-refresh).
- Re-check, sign-in, Create/Reset dialogs use the same in-control morph;
  `CanExecute` gating keeps duplicate dispatch impossible (buttons disable).
- Load-more busy not directly exercised: with <25 documents `HasMore` is
  false; the morph XAML is the shared pattern (verified by inspection).

## Part 4 — Feedback (U4) — PASS

- Copy chat message → "Copied / Message copied to clipboard."
  (`11-copy-snackbar.png`).
- QueryDetail prompt/answer/citation copies → snackbars, incl. "Citation
  copied to clipboard." (`13-querydetail-copy.png`).
- Settings URL + identity copies → snackbars, incl. "Signed-in account
  copied to clipboard." (`24-settings-copy-user.png`, `23-…` post-dismiss).
- Download success → "Downloaded ux-polish-notes.txt" + file opens in
  Notepad (`28-download-inflight.png`, `29-download-done.png`).
- Download failure (API down) → row ring clears, shared status keeps the
  existing error mapping (`32-download-failed.png`,
  `33-download-failure-outcome.png`).
- Sign out → in-window "Sign out of this session?" Yes/No
  (`59-signout-confirm.png`); No leaves the session untouched
  (`60-signout-declined.png`, still walker); Yes signs out (`61-signed-out.png`).
- Cancel in-flight upload (60 MB batch, API process suspended to hold the
  transfer open): "Cancel the in-flight upload? Files already uploaded stay
  uploaded." with "Yes, cancel" / "Keep uploading"
  (`50-upload-suspended.png`, `51-upload-cancel-confirm.png`); declining
  resumes the upload view (`52-upload-decline-resumes.png`); accepting closes
  the dialog and the cancelled file never lands server-side (verified via API
  document list).
- Reset password → in-window confirm "Reset the password for 'walker'?"
  (`19-reset-password.png`); declining leaves everything untouched
  (`20-reset-declined.png`). Full reset submission was deliberately **not**
  executed (would invalidate the walkthrough account).
- CreatePerson renders as an in-window ContentDialog (single OS window,
  `18-create-person.png`); dismissed via Escape.

## Part 5 — Conversation (U5) — PASS

- "Ask again" from History populates Ask input, nothing auto-sends
  (`08-ask-again-from-history.png`); same from the saved-answer page
  (`14-ask-again-from-detail.png`, conversation + populated input, send idle).
- 4 exchanges verified (1 pre-seeded via REST + 3 via UI): full cited answer
  (`10-ask-answered.png`), second cited answer (`41-ask-second-answer.png`),
  and the exact `no relevant content found` path (`42-ask-third-answer.png`).
  Newest message auto-visible every time (input bar in frame, no manual
  scroll). 10+ exchanges were not run (per-exchange mechanism identical;
  ~25 s Ollama latency each).
- Clear (`QueryClearConversationButton`) empties to the empty state
  (`15-ask-cleared.png`); one Tab lands in the input. Strict
  auto-focus-input on clear is **not** implemented — follow-up below.

## Part 6 — Small window (U6) — PASS

- Window resized to 820×640 (≈800×600 client): Creator/Created collapse,
  Filename/Type/Size/Status + actions fit; no `ScrollBar` element in the UIA
  tree, i.e. no forced horizontal scroll (`55-library-small.png`,
  `56-library-small-right.png`); row download/delete buttons measure 44×44
  (`(778,196 44x44)`, `(822,196 44x44)` in physical px).
- Widened past 720 content DIPs: all columns return (`58-library-wide-again.png`).
- Viewer (non-admin) on the emptied library: "No documents yet / Ask a
  question to get started, or contact an admin to add documents." — never
  "Upload" (`64-viewer-library-empty.png`); no upload button, no Admin nav.

## Part 7 — Login & Settings (U7) — PASS

- Enter in `UsernameBox` submits (validation error via shared idiom,
  `03-login-enter-username.png`); Enter in `PasswordBox` signs in (walker and
  viewer sessions).
- Settings re-check: status row re-renders with outcome + last-checked
  moment (`23-settings-copy-url.png` shows "Connected / Last checked
  10/2/2026 7:00 PM"); both values copy with snackbars.
- Busy/error on Login use the shared idioms (fixed Sign in button, `02-…`,
  `62-…`).

## Part 8 — Accessibility/theme (U8) — PASS

- Keyboard: Library Tab order Refresh → PageSize → pager → nav toggle →
  nav items (logical); focus tooltip appears (`65-focus-visible.png`); Clear
  → one Tab reaches the chat input.
- Dark theme legible on Dashboard/Ask/Library
  (`25-dashboard-dark.png`, `26-ask-dark.png`, `27-library-dark.png`).
  Light covered everywhere else. High Contrast has no in-app switch
  (Light/Dark radios only) — not exercised.
- Frozen IDs: all U8.1 IDs verified present **except** the contract's
  `QueryDetailCopy{Prompt,Answer,Citation}Button` names — the implementation
  (predating 028, cf. `021-wpfui-modernization/data-model.md`) uses
  `Copy{Prompt,Answer,Citation}Button`. Contract drift, not a regression —
  follow-up below. New U8.2 IDs (`HistoryRetryButton`,
  `DocumentsMineRetryButton`, `AdminRetryButton`,
  `QueryClearConversationButton`, `SettingsRecheckConnectionButton`,
  `SettingsCopyWorkstationUrlButton`, `SettingsCopySignedInAsButton`,
  `LoginStatusMessage`, …) all exercised live above.
- Regression gates (T040/T041) were green before this walkthrough; the only
  product change since is the crash fix below (rebuilt + re-verified live).

## Defects found

1. **Fixed (ba55620) — `ui:Button.Icon` must be a `SymbolIcon`; busy-morph
   `Grid`s crashed page loads.** `LoginPage` Sign in button and the shared
   `StatusFooterControl` retry button (i.e. **every** async page) threw
   `XamlParseException` at load; the app could not leave the blank shell.
   Fixed by using StackPanel content (morph Grid + label), matching the
   Library-refresh pattern. Also replaced two `{StaticResource {x:Type …}}`
   `BasedOn` lookups (`SymbolIcon`/`ProgressRing` have no such resource) with
   the codebase-standard visibility converters. Rebuilt, relaunched, entire
   walkthrough ran on the fixed binary. Follow-up: add a smoke test that
   instantiates every page/dialog (build alone cannot catch this class).
2. **Follow-up — Dashboard empty state points non-admins at uploading.**
   Viewer Dashboard says "Open the library to upload your first document."
   (`63-viewer-dashboard.png`) although Employees cannot upload. T035 scoped
   role-awareness to the Library empty state (which passes); extend the same
   treatment to `DashboardEmptyState`.
3. **Follow-up — Clear conversation should focus the input** (U5.3 second
   half). Empty state shows; focus stays on Clear (one Tab away).
4. **Follow-up — contract U8.1 ID names.** `ui-contracts.md` lists
   `QueryDetailCopy{Prompt,Answer,Citation}Button`; the frozen, pre-existing
   IDs are `Copy{Prompt,Answer,Citation}Button` (renaming them would violate
   FR-022). Correct the contract text.
5. **Observation (not a defect) — theme override.** The Dark selection applied
   and rendered correctly, but the app was back on Light later in the session
   without a restart and without further theme input (possibly OS/session
   dynamics in this shared remote desktop). Verify theme stickiness on a
   stable machine if it matters.

## Not exercised (with reason)

- Load-more busy ring: needs >25 documents (`HasMore` false here).
- Full password-reset submission: would invalidate the walkthrough account;
  confirm/decline paths verified.
- 10-exchange conversation: 4 exchanges verified; per-exchange auto-scroll
  identical, ~25 s Ollama latency each.
- High Contrast theme: no in-app switch (Light/Dark only).
- Ask-again from a cold Ask page (Ask never visited): History path verified;
  same singleton read-on-Loaded mechanism.
- Re-check/sign-in ring captures: sub-second locally; morph XAML is the
  shared verified pattern.

## Expected outcomes — actual results

| Check | Contract | Pass condition | Result |
|-------|----------|----------------|--------|
| Header parity | U1 | 8/8 share one header; Dashboard profile on post-header row; single-language copy | PASS |
| Status + retry | U2 | one InfoBar idiom; retry on every async page; wrapped long errors; zero native message boxes | PASS |
| Busy | U3 | 100% in-control; conversation visible while answering; no duplicate dispatch | PASS (load-more ring by inspection) |
| Feedback | U4 | 100% copy confirms; download in-flight/success/failure; sign-out + cancel-upload confirm | PASS (reset-submit not executed) |
| Conversation | U5 | auto-scroll every exchange; ask-again populates every time; clear works | PASS (focus-input resolved post-walkthrough) |
| Small window | U6 | no h-scroll at 800×600; ≥44-DIP targets; role-aware empty states | PASS (Dashboard role-awareness resolved post-walkthrough) |
| Login & Settings | U7 | Enter submits from both boxes; re-check works; values copyable | PASS |
| Identity/theme/spacing | U8 | frozen IDs preserved + new stable IDs; shared Thickness; tokens only; themes legible | PASS (contract ID text corrected post-walkthrough) |
| Regression gates | U8 | CSharpier, build, unit/contract/offline-integration green | PASS per T041 + live re-verify after ba55620 |

## Follow-up resolutions (2026-10-02, post-walkthrough)

All four walkthrough findings were addressed after the evidence run; build
(0 errors) and the unit suite (365/365) re-verified green after each change:

1. **Dashboard role-aware empty states** — both "No documents yet" bodies
   (whole-dashboard and recent-documents panel) now show admins the upload
   line and viewers "Ask a question to get grounded answers, or contact an
   admin to add documents." (same `IsAdmin` + converter pattern as Library
   T035; FR-019/SC-010 now hold on Dashboard too).
2. **Clear focuses the input** — the Clear action now routes through
   `OnClearConversationClicked` (QueryPage code-behind): executes
   `ClearConversationCommand`, then calls the new `ChatControl.FocusInput()`
   so the input is immediately ready for the next question (U5.3).
3. **Contract U8.1 ID text corrected** — now lists the frozen
   `CopyPromptButton` / `CopyAnswerButton` / `CopyCitationButton` IDs
   (pre-existing; renaming them would violate FR-022).
4. **Theme-override observation** — left as the noted stable-machine
   verification item (not a defect; no in-app High Contrast switch exists).

## Convergence verification (2026-10-05, T046)

Second walkthrough pass covering the T045 smoke gate plus every residual
item from "Not exercised" / "Follow-up resolutions" above. Verdict: **all
items PASS except High Contrast, which remains honestly not-exercised**
(reason below). The earlier "PASS (…follow-up)" rows are now fully PASS
with no caveats, except where noted.

### Environment (same shape as the first pass)

- **API**: same exe/CWD/ports as above (PID 19132); rebuilt client only —
  `RAGGit.Client.WPF` rebuilt 2026-10-05 from current source (includes
  `edea5f4` post-walkthrough fixes; the 10-02 binary predated them by
  ~1 min). Full-solution rebuild otherwise clean; the only build errors
  were MSB3021/3027 copy-locks on the *running API's* DLLs (known
  gotcha: stop the API before building it — the client DLL built fine).
- **Client**: PID 6416, output `appsettings.json` still carries
  `walk-admin-key` (rebuild did not overwrite it). Accounts `walker`
  (Admin) / `viewer` (Employee), `Passw0rd!` — both re-verified via
  `POST /api/auth/login` (HTTP 200) before driving the UI.
- **Seed data**: library EMPTY (0/0 entries, admin copy "Upload a document
  to see it here." confirmed for walker in `76`-era Library shot);
  walker history seeded to **26 items** for the load-more check (21 via
  `POST /api/queries` as walker — empty-library asks answer in
  ~111–151 ms with `no relevant content found`, no generation — plus 5
  from UI/previous runs). Dev-DB-only side effect; integration suites use
  their own output-dir DBs.
- **Driver friction (shared console session)**: the app window was
  repeatedly minimized to 160×28 by session dynamics (5+ restores via
  ShowWindow/SetForegroundWindow) and `send-input`/screen-capture refuse
  when not foreground. UIA `search`/`wait-for`/`invoke`/`set-value` work
  backgrounded; all gates below re-ran green after restores. The first
  pass's "theme revert" observation was NOT reproduced (see F).

### A. Page-load smoke gate (T045) — PASS with one gate-text fix

Page gates 8/8 PASS as walker (`smoke-pages.ps1`: Dashboard, Library,
Ask, History, MyDocs, Admin, Settings, plus History → ViewQueryButton →
saved-answer `AskAgainButton` — walker's prior history made this
runnable with no extra setup):

- **Dialogs**: CreatePerson opens (`AddPersonButton` → `NewDisplayNameBox`
  found in 202 ms), renders fully in-window (`77-t046-create-person.png`),
  Escape dismisses it (`NewDisplayNameBox --gone`), single window remains
  — PASS. UploadDialog opens as its own window ("Upload Documents"),
  renders drop zone + queue empty state + Upload/Close footer
  (`75-t046-upload-dialog.png`), Close dismisses with no confirm (nothing
  in flight — correct), single window remains — PASS. The
  cancel-mid-upload confirm path stays covered by the first pass
  (`50/51/52-…`); it was not re-run (no in-flight upload staged).
- **Gate-text defect (docs, fixed separately)**: the quickstart line
  `wait-for "UploadDropArea"` can never succeed — the AutomationId sits
  on a `Border`, which gets no UIA peer (full-tree inspect of the dialog
  shows only the inner "Drop files here or browse…" text, the
  `AddFilesButton`, Upload and Close buttons). The gate now waits for
  `AddFilesButton` instead (same crash-class coverage: dialog
  instantiated and interactive). Static frozen-ID audit still passes
  (the ID exists in XAML).
- Sign-in itself exercised the keyboard path: transient UIA flakiness at
  cold start (`wait-for UsernameBox` passed, then `set-value` failed) —
  after the tree settled, `set-value` + `invoke SignInButton` signed in
  cleanly (`70-t046-login-start.png`).

### B. Viewer Dashboard empty state (FR-019/SC-010 fix) — PASS

- As viewer on the empty library: whole-dashboard state reads "No
  documents yet / Ask a question to get grounded answers, or contact an
  admin to add documents." (`81-t046-viewer-dashboard.png`); UIA search
  for `pload` (Upload/upload) returns **0 matches** — the word never
  appears. (Panels collapse when fully empty, so only this state shows.)
- After asking one question as viewer (`no relevant content found`,
  ~15 s via UI): "Recent documents" panel appears with its own "No
  documents yet" + the same viewer copy (`83-t046-viewer-dashboard-queried.png`),
  and "Recent questions" lists the asked question — both panel-level
  empty-state bodies verified live.
- Contrast as walker (Admin): recent-documents panel reads "Open the
  library to upload your first document." (`84-t046-walker-dashboard.png`).

### C. Clear-then-input-focus (U5.3 fix) — PASS

In Ask with one exchange (`86-t046-ask-before-clear.png`), clicking
`QueryClearConversationButton` empties to the empty state with Dark
applied (`87-t046-ask-after-clear.png`, caret visible in the input);
untargeted `send-keys "focusprobe"` then lands **directly** in
`ChatInputBox` (`get-value` → `"focusprobe"`, `88-t046-ask-focus-proof.png`).
Probe text removed afterwards (input verified empty).

### D. Load-more busy morph (FR-004/U3.3) — PASS, exercised live

History/MyDocs page size is 20 (`HistoryViewModel._limit = 20`,
`DocumentsMineViewModel._limit = 20`; Library default 25). With 26
walker questions, `LoadMoreButton` is present (`89-…-before.png`,
Q21–Q17). Scrolling needed workarounds: wheel over the header scrolls
the nav/content unreliably, `scroll-into-view` reports no scrollable
ancestor — `focus "LoadMoreButton"` scrolls it into view
(`95-t046-history-focused.png`, idle ↓ arrow visible).

- With the API process suspended (frozen mid-request, same technique as
  the first pass's upload test), invoking LoadMore holds `IsBusy`: the
  header refresh morphs to a spinning ring with content visible
  (`92-t046-history-loadmore-busy.png`), AND the Load More button itself
  morphs ↓-arrow → blue ProgressRing, label intact
  (`96-t046-history-loadmore-ring.png`) — the exact shared-pattern morph
  from `HistoryPage.xaml:180-193`. Content never blanks.
- API resumed → 6 remaining items append (Q1 visible), `HasMore=false`,
  `LoadMoreButton --gone` (`97-t046-history-loadmore-done.png`).

### E. High Contrast legibility (FR-024) — still not exercised (reason)

No in-app HC switch exists (Settings offers Light/Dark radios only —
`79/80/98-…`); OS High Contrast is OFF (`SPI_GETHIGHCONTRAST dwFlags=126`,
bit 0 clear; no HC scheme in the registry). Enabling it would flip the
display of this **shared, owner-active console session** for everyone
and require an app restart + full re-walk to be meaningful — not
imposed. Structural risk stays low: T040 audit bans hard-coded colours
(token-only rendering) and Dark/Light are both verified legible
(`25/26/27-…`, `87/88/96/97/98-…`).

### F. Theme stickiness — PASS (earlier observation not reproduced)

Dark selected in Settings at 11:43:02. Still Dark at 11:52+ across
Dashboard → Library → Ask → History → Settings navigation, repeated
minimize/restore cycles, and API suspend/resume (`98-t046-dark-persist.png`
shows the Dark radio selected on a fully Dark Settings page; every
screenshot since 11:43 renders Dark). The first pass's Light-revert is
attributed to the same session dynamics that minimize background
windows here — no product defect indicated.

### Defects / follow-ups from this pass

1. **Fixed (docs, separate commit)** — smoke-gate `UploadDropArea`
   wait-for replaced with `AddFilesButton` (Border has no UIA peer; see A).
2. **Follow-up (pre-existing, not 028)** — `LoadMoreButton`
   (History/DocumentsMine) exposes an AutomationId but no
   `AutomationProperties.Name` (UIA shows a nameless Button + a separate
   "Load more" text). Screen-reader users get the text, but the button
   itself is unnamed. Consider adding `Name="Load more"` if those pages
   are ever touched again. [Priority: Low]
   (Ref: `src/RAGGit.Client.WPF/Views/Pages/HistoryPage.xaml:169-179`,
   `DocumentsMinePage.xaml:143-151`)

## Not exercised (updated 2026-10-05 — items now covered struck through)

- ~~Load-more busy ring: needs >25 documents (`HasMore` false here).~~
  Exercised live (D): 26 seeded questions, History limit 20, ring
  captured on the button itself.
- ~~Viewer Dashboard empty state / clear-then-input-focus: post-walkthrough
  fixes.~~ Both verified live (B, C).
- Full password-reset submission: would invalidate the walkthrough account;
  confirm/decline paths verified.
- 10-exchange conversation: 5 exchanges verified (4 first pass + 1 this
  pass); per-exchange auto-scroll identical, ~25 s Ollama latency each.
- High Contrast theme: no in-app switch (Light/Dark only); OS-level toggle
  requires changing the shared console session's display — not imposed (E).
- Ask-again from a cold Ask page (Ask never visited): History path verified;
  same singleton read-on-Loaded mechanism.
- Re-check/sign-in ring captures: sub-second locally; morph XAML is the
  shared verified pattern.
- ~~Theme stickiness:~~ Dark persisted ~9+ min across pages (F); earlier
  revert attributed to session dynamics.
