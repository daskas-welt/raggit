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
