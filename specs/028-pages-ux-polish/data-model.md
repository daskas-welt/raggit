# Data Model: All-Pages UX/UI Polish

**Feature**: `028-pages-ux-polish` | **Date**: 2026-10-02 | **Plan**: [plan.md](plan.md)

No persisted entities are added or changed. Everything below is transient view state,
in-memory seams, or derived layout state. No table, field, DTO, API shape, stored
preference, or contract changes.

## Explicitly out of scope

The workstation API, all API clients, all contracts, the SQLite/LanceDB stores, and the
page-size preference are read-only. The two paging paradigms stay per-surface
(spec Assumption). The conversation store's persistence semantics are untouched —
`ClearConversation` clears the in-session view of the current conversation exactly as
`ReplaceAll` does today, and adds no new deletion API.

## Entity: Transient notification (NEW seam, in-memory)

| Field | Rule |
|-------|------|
| Title | Short, plain ("Copied", "Downloaded", "Signed out") |
| Message | One line, plain product copy |
| Kind | Success \| Information \| Danger — maps to the snackbar's control appearance |
| Lifetime | Auto-dismisses (~5 s); never blocks or displaces content |

`INotificationService` (in `Client.Core.Services`) with an in-memory double recording
calls for unit tests; `WpfNotificationService` (WPF project) wraps the already-wired
WPF-UI snackbar. Callers: copy affordances, download outcomes, sign-out outcome, Settings
copy rows.

## Entity: Pending ask (NEW seam, in-memory)

| Field | Rule |
|-------|------|
| PendingPrompt | `string?` — the original question text |
| Write | By History/QueryDetail immediately before navigating to Ask |
| Read + clear | Once, by QueryPage's `Loaded`; populate `QueryText`, never auto-send |
| Lifetime | Cleared on first read; null on every later visit |

Mirrors the existing `QueryDetailNavigationState` singleton exactly.

## Entity: Per-document download state (transient, NEW on `LibraryViewModel`)

| Field | Rule |
|-------|------|
| Busy set | The document ids whose download is in flight; one row's button morphs to a ring |
| Success | Snackbar "Downloaded \<filename\>"; file opens from `%TEMP%\RAGGit\` as today |
| Failure | Existing `ErrorMessage` mapping (unavailable / cannot reach / timeout) + snackbar |
| Concurrency | Other rows stay actionable; page-level `IsBusy` untouched |

## Entity: Connection status (transient, becomes updatable)

`WpfConnectionState` (today a one-shot startup snapshot) gains change notification and a
`LastCheckedAtUtc`; the re-check action calls the existing `GET /api/auth/me` and writes
the outcome. The severity/icon mapping (`PlugConnected24` / `PlugDisconnected24`,
success/critical brushes) and the "cannot reach AI workstation" pattern stay as-is.

## Entity: Conversation state (transient, extended)

| Field | Rule |
|-------|------|
| `IsBusy` | Drives the send-button morph and the input-bar status row (in-stream busy) |
| `ClearConversationCommand` | Clears `Messages` and the shared conversation store's in-session messages; input regains focus |
| Auto-scroll (behavior) | After every send or arrival the newest message is brought into view — the rule of FR-010, with no "reading upward" exception in this feature |

## Entity: Layout mode (transient, derived, per page)

| Page | Wide | Compact (content width ≤720 DIPs) |
|------|------|-----------------------------------|
| Dashboard | Panels side by side (027, unchanged) | Panels stacked (027, unchanged) |
| Admin | Table + 320 detail column (026, unchanged) | Detail stacks (026, unchanged) |
| Library (NEW) | All columns; `MinWidth` floor 880 | Creator + Created columns hidden; floor lowered so Filename/Status/actions fit without horizontal scrolling |

## Entity: Role-aware empty-state copy (transient, derived)

| Viewer | Library empty-state hint |
|--------|--------------------------|
| Admin | Upload guidance (as today) |
| Non-admin | Guidance referencing an action they can take (e.g. ask a question) — never "Upload" |

## State transitions

- Copy activated → clipboard write → success snackbar → auto-dismiss (~5 s).
- Download activated → row button busy (ring) → success snackbar + file opens, or failure
  snackbar/status with the existing message mapping; the busy set always empties.
- Sign-out activated → confirm dialog → "No" leaves the session untouched; "Yes"
  clears the token store and navigates to Login (no outcome snackbar — the session
  is gone).
- "Ask again" activated (History or QueryDetail) → prompt written to pending-ask state →
  navigate → Ask page `Loaded` reads + clears state → `QueryText` populated, not sent.
- Question sent → send button busy + input-bar status row → answer arrives → newest
  message auto-scrolled into view → busy clears.
- Clear conversation activated → messages cleared in view and store → empty state shows →
  input focused.
- Connection re-check activated → re-check button busy → `GetAuthMeAsync` → status row
  re-renders with outcome + `LastCheckedAtUtc`; failure strings unchanged.
- Library content width crosses 720 DIPs → secondary columns collapse/restore; header,
  rows, actions, and paging unaffected.
- Load fails on any async page → shared status treatment with retry → retry re-runs the
  load; busy stays in-control; last-known-good content remains (027 gating preserved).
