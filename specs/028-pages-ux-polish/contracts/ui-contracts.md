# UI Contracts: All-Pages UX/UI Polish

**Feature**: `028-pages-ux-polish` | **Date**: 2026-10-02 | **Plan**: [../plan.md](../plan.md)

Desktop presentation only; no external API. The observable contracts are the layout,
status, feedback, and identity invariants below, checked by the static audits and the
walkthrough in [../quickstart.md](../quickstart.md). FR/SC references are to
[../spec.md](../spec.md).

## U1 — One header treatment on every content page (FR-001, SC-001)

1. Every content page — Dashboard, Library, Ask, QueryDetail, History, My Documents,
   Admin, Settings — opens with the same header structure: a header card with the page
   title, a one-line supporting description on the left, and the page's actions on the
   right.
2. The Dashboard header adopts this structure; its 027 content below (six metric cards,
   panels, empty states) is unchanged, and the profile card sits on a post-header row
   (library summary left, profile card right).
3. Login keeps its centered-card idiom (Story 5 grants busy/error parity only).
4. The QueryPage header's actions include the Clear-conversation action; the suggestion
   chips keep their meaning with single-language plain copy.

**Prohibited**: a page-specific header variant; a bilingual or mixed-language string; a
quick action competing with the title row on any content page.

## U2 — One status idiom with retry everywhere (FR-002, FR-003, FR-015, SC-002, SC-004, SC-008)

1. Persistent status/error messages on every page and dialog use one severity-styled
   presentation (InfoBar-based shared component): severity (information/success/warning/
   danger), message, and — on every asynchronously loading page — a retry action.
2. Status text wraps; it is never trimmed or ellipsized on any page.
3. Load failure on History, My Documents, Admin, Login, Dashboard, Library, and Ask
   pairs the shared status treatment with a retry action that re-runs the failed load.
4. No operating-system-native message box appears anywhere in the client.

**Prohibited**: the hand-rolled Admin status strip; the bare critical TextBlock on Login;
an error footer without retry; `TextTrimming` on status text; `MessageBox.Show`.

## U3 — One busy rule (FR-004, FR-011, SC—via U-walkthrough)

1. Action-initiated busy shows in the initiating control (icon→ProgressRing morph,
   control disabled) while page content stays visible — refresh, load-more, download
   rows, re-check, sign-in, send, reset-password, create-person.
2. Initial page content loads keep the shared centered loading treatment.
3. While a question is in flight, the Ask conversation stays visible: the send button is
   busy and a status row sits with the input bar; the page-covering ring is gone.
4. Duplicate dispatch stays impossible (`CanExecute` gating visible to the user).

**Prohibited**: a page-covering busy overlay on any action-initiated operation; a
page-specific busy variant; an enabled control while its own operation is in flight.

## U4 — Every action acknowledges itself (FR-006, FR-007, FR-008, FR-009, SC-003)

1. Every copy affordance (chat messages, QueryDetail prompt/answer/citation, Settings
   URL/identity) shows a brief auto-dismissing confirmation and never blocks content.
2. Downloads communicate in-flight (row button), success (snackbar + file opens), and
   failure (snackbar/status with the existing message mapping).
3. Sign-out and cancel-during-upload request confirmation through the in-window dialog
   pattern; declining leaves the session/upload untouched.
4. CreatePerson and ResetPassword render as in-window dialogs (existing dialog host);
   ResetPassword's confirmation uses the in-window confirm, not a native message box.
5. UploadDialog remains a separate window with aligned chrome: window icon, resize/
   taskbar flags, and button minimum widths drawn from the shared scale.

**Prohibited**: a silent clipboard write; a download with no in-flight or outcome
signal; an unconfirmed sign-out; a native confirmation dialog; unaligned dialog chrome.

## U5 — The conversation feels finished (FR-010, FR-012, FR-013, SC-006, SC-007)

1. After every send or arrival, the newest message is visible without manual scrolling
   (including answers with long text or many citations).
2. "Ask again" from History and from the saved-answer page populates the Ask page with
   the original question on every attempt — including when the Ask page was never open.
   The question is populated, not auto-sent.
3. The Clear-conversation action empties the conversation (view and in-session store),
   shows the empty state, and focuses input.

**Prohibited**: a lost prompt preset; auto-send on ask-again; a clear that leaves stale
messages in the store's session view.

## U6 — Data surfaces at any window size (FR-014, FR-016, FR-019, SC-005, SC-009, SC-010)

1. Row-level action controls meet the 44-DIP target on every data surface (Library
   rows rise from 36×32; the standard elsewhere is unchanged).
2. At content widths ≤720 DIPs, the Library table hides its secondary columns
   (Creator, Created) so Filename, Status, and row actions are usable without horizontal
   scrolling at 800×600; wider widths restore all columns. Dashboard/Admin keep their
   existing 720 behaviors.
3. Empty-state guidance references only actions the current user has: non-admins on an
   empty Library never see upload guidance.

**Prohibited**: a forced 880-DIP floor at the minimum window; an empty state pointing a
non-admin to an admin-only action.

## U7 — Login and Settings parity (FR-017, FR-018)

1. Enter in the username or password box submits sign-in (when not busy); sign-in busy
   and error use the shared in-control busy and status idioms.
2. Settings carries the standard header card, a re-check action for the connection
   status (in-control busy, outcome re-renders the status row with a last-checked
   moment), and copy buttons for the workstation address and signed-in identity.

**Prohibited**: an inline ring below the sign-in button; a stale-only connection status
with no re-check; an uncopyable displayed value where a copy control exists.

## U8 — Identity, spacing, and theme (FR-020, FR-021, FR-022, FR-023, FR-024, SC-011, SC-012)

1. All pre-existing automation IDs remain present with unchanged meaning, including
   the 16 Dashboard IDs (`DashboardProfileCard`, `DashboardRefreshButton`,
   `DashboardMetric{Documents,Ready,Indexing,Failed,Queries,Mine}`, `DashboardTile{History,MyDocs,Admin}`,
   `DashboardEmptyState`, `DashboardLibraryButton`, `DashboardAskButton`,
   `DashboardStatusBar`, `DashboardRetryButton`), `LibraryRetryButton`,
   the `AskAgainButton` IDs on HistoryPage and QueryDetailPage, `QueryDetailCopy{Prompt,Answer,Citation}Button`,
   `SuggestionChips`/`SuggestionChipButton`, all Settings row IDs, `SignOutButton`,
   `UsernameBox`/`PasswordBox`/`SignInButton`, and the Admin IDs (`AddPersonButton`,
   `RefreshUsersButton`, `AdminStatusBar`, `UserSearchBox`, `RoleFilterCombo`,
   `StatusFilterCombo`, `UsersList`, `Sort{People,Role,Status}Header`,
   `ClearUserFiltersButton`, `SelectedPersonDetails`, `OpenResetPasswordButton`).
2. New stable automation IDs: `HistoryRetryButton`, `DocumentsMineRetryButton`,
   `AdminRetryButton`, `QueryClearConversationButton`, `SettingsRecheckConnectionButton`,
   `SettingsCopyWorkstationUrlButton`, `SettingsCopySignedInAsButton`,
   `LoginStatusMessage`, and the shared status component on every adopting page.
3. Spacing, padding, and sizing values on affected surfaces resolve to shared
   Thickness resources on the documented 4-px scale (header card `16,12`, page paddings,
   empty-state body margin `0,12,0,0`, dialog button minimum widths) — no ad-hoc
   literals on affected surfaces.
4. All text/colour emphasis uses theme tokens and shared styles only (023 rules hold):
   no colour literals, no `Opacity=`, no ad-hoc `FontSize`, valid icon/theme keys;
   Light/Dark/High Contrast legible with nothing clipped at 800×600.
5. The shared behavior layer changes only where the spec's Assumptions allow affordance
   wiring (notification seam, pending-ask state, clear-conversation command, per-download
   busy, connection re-check); the workstation API and all contracts are untouched.

**Prohibited**: renaming/removing a frozen ID; an inline spacing literal on an affected
surface; a behavior or contract change beyond the listed affordances.
