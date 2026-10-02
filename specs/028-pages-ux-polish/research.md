# Research: All-Pages UX/UI Polish

**Feature**: `028-pages-ux-polish` | **Date**: 2026-10-02 | **Plan**: [plan.md](plan.md)

Grounded in a full-client UI audit (2026-10-02) and two codebase research passes
(R1–R8 below cite `file:line` as of today). All four spec clarifications are encoded.

## Decision: Snackbar confirmations via a Core seam (R1)

**Decision**: Add `INotificationService` (interface in `RAGGit.Client.Core.Services` with a
kind parameter — success / information / danger) implemented by `WpfNotificationService`
wrapping the already-registered, already-wired WPF-UI `ISnackbarService`
(`App.xaml.cs:118`, `MainWindow.xaml.cs:39`); register it alongside `IDialogService`
(`App.xaml.cs:127`) and consume it from pages/code-behind and ViewModels.

**Rationale**: The shell's `SnackbarPresenter` is wired but no code ever calls `Show` — the
cheapest possible feedback channel already exists. VMs cannot reference WPF-UI types
(framework-independent `Client.Core`), and the repo already has the exact seam pattern
(`IDialogService`/`ILauncherService`/`IFilePicker`: Core interface, `Wpf*` adapter,
singleton registration, in-memory test double).

**Alternatives considered**: calling `ISnackbarService` directly from code-behind only
(leaves ViewModels — where download/copy outcomes live — unable to confirm; the per-call
`App.Services` dance in VMs breaks the seam pattern); a custom toast overlay (duplicates a
working component).

## Decision: One InfoBar-based status idiom via a shared component (R6)

**Decision**: Replace the four status idioms (icon+text footer grids on Library/History/
Mine/QueryDetail, `ui:InfoBar` on Dashboard, hand-rolled `Border` strip on Admin, bare
critical `TextBlock` on Login) with one shared `StatusFooterControl` built on `ui:InfoBar`
(severity, message, optional retry action), used by every asynchronously loading page and
the Login card. Message text wraps (never `TextTrimming`). Every async page pairs load
failure with a retry action.

**Rationale**: `ui:InfoBar` is already the severity-styled idiom on the most recently
redesigned surfaces (Dashboard, QueryPage, UploadDialog) and carries an action-content
slot for Retry; the current icon+text grids are copy-paste-identical in three pages with
drifting wrap/trim behavior (History truncates, Library wraps) — a shared component ends
both the duplication and the drift at once.

**Alternatives considered**: a shared Style only (styles cannot carry the retry
command/slot per page); standardizing on the icon+text grid instead (moves Dashboard/
QueryPage backwards and has no severity model).

## Decision: In-control busy everywhere; in-stream busy for chat (R3)

**Decision**: Action-initiated busy shows in the initiating control (the Dashboard
refresh button's icon→`ui:ProgressRing` morph pattern, control disabled), page content
always stays visible; initial page content loads keep the centered loading treatment.
QueryPage's page-covering ring (`QueryPage.xaml:88-96`) is removed — the send button
morphs and a small status row sits with the input bar while a question is in flight.
`IsBusy` gating in `CanExecute` stays as the duplicate-dispatch guard.

**Rationale**: Settled by spec clarification Q4. The send path already gates on
`QueryViewModel.IsBusy` (`QueryViewModel.cs:97-101`); only the presentation (hiding the
page behind a ring) violates FR-011.

**Alternatives considered**: keep the page-covering overlay on non-chat pages
(page-specific variant — exactly what FR-004 forbids); per-surface choice (status quo).

## Decision: Reliable "Ask again" via a pending-prompt state singleton (R2)

**Decision**: Add `AskNavigationState` (DI singleton holding `string? PendingPrompt`),
mirroring the proven `QueryDetailNavigationState` pattern (`QueryDetailNavigationState.cs:10-13`,
set by `WpfNavigationService.cs:39-43`, read-and-cleared by the target page's `Loaded`).
History/QueryDetail write the original prompt before navigating to Ask; `QueryPage`'s
`Loaded` reads it, clears it, and populates `QueryText` (populate only — no auto-send,
per FR-012). Delete both broken best-effort presets (`QueryDetailPage.xaml.cs:34-46`,
`HistoryPage.xaml.cs:45-73`).

**Rationale**: Pages are transient and recreated per navigation
(`App.xaml.cs:234-246`), so setting `QueryText` on a freshly resolved ViewModel is
discarded — the working precedent for exactly this problem already ships in the app
(`QueryDetailNavigationState`). `QueryViewModel.ReaskAsync` exists but auto-sends and is
only used by tests; populate-only matches FR-012.

**Alternatives considered**: `Navigate(Type, dataContext)` overload (replaces the page
DataContext — explicitly avoided by the existing pattern's doc comment); auto-send via
`ReaskAsync` (stronger than the spec requires and surprises the user).

## Decision: Form dialogs go in-window; Upload stays a window (R8)

**Decision**: Convert CreatePersonDialog and ResetPasswordDialog from modal `Window`s
to `ContentDialog`s shown through the existing `IContentDialogService` host
(`RootContentDialogHost`, `MainWindow.xaml:59`), keeping their ViewModels; the raw
`MessageBox.Show` in `ResetPasswordDialog.xaml.cs:28-34` is replaced by
`IDialogService.ConfirmAsync` (which then works, since the confirm no longer needs to
appear over a separate window). UploadDialog stays a separate window — its chrome is
aligned (window icon, `ResizeMode`/`ShowInTaskbar` flags, button minimum widths) and
cancel-mid-upload gains a confirm.

**Rationale**: Spec clarification Q3. The two form dialogs are small focused forms that
map directly onto the existing host; Upload is a complex multi-step surface (drop zone,
queue, per-file progress, auto-close logic at `UploadDialog.xaml.cs:52-114`) where a
window is defensible, so it gets alignment, not conversion.

**Alternatives considered**: convert all three (Upload adds risk with little perceived
gain — rejected in Q3); convert none (leaves two modal idioms and the MessageBox).

## Decision: Per-download in-control busy + outcome notifications (R4)

**Decision**: `LibraryViewModel` tracks per-document download state (a busy set exposed
for row binding); the row download button morphs icon→ring while *that* document
downloads; success and failure produce snackbar confirmations via the notification seam
(failure keeps the existing `ErrorMessage` mapping at `LibraryViewModel.cs:340-364`).
Downloads land unchanged in `%TEMP%\RAGGit\` via `WpfLauncherService.OpenAsync`
(`WpfLauncherService.cs:15-31`).

**Rationale**: FR-004/FR-007. `DownloadAndOpenAsync` exists
(`LibraryViewModel.cs:326-365`) but has no busy or success signal — the busy set is the
minimum VM wiring the affordance needs (allowed by spec Assumptions), and the row-level
morph follows the app-wide busy rule.

**Alternatives considered**: a single page-level `IsBusy` for downloads (blocks
unrelated rows and hides which document is in flight); no busy state (status quo).

## Decision: Library table degrades at the established breakpoint (R7)

**Decision**: At content widths ≤720 DIPs (the established sibling breakpoint —
`DashboardPage.xaml.cs:37`, `AdminUsersPage.xaml.cs:187`), the Library table collapses
its secondary columns (Creator, Created) and lowers the `MinWidth` floor
(`LibraryPage.xaml:84`) so Filename, Status, and the row actions stay usable without
horizontal scrolling at 800×600; wider widths restore all columns. Row action buttons
rise to the 44×44 standard (today `36×32` at `LibraryPage.xaml:220-249`). Implemented
page-owned (same `SizeChanged` approach as Dashboard/Admin), keeping the shared
`ColumnDefinitions` string intact per state.

**Rationale**: The fixed `MinWidth="880"` guarantees horizontal scrolling at the
minimum window; the ≤720 two-state switch is the app's established responsive pattern,
so the table joins the convention rather than inventing a per-page one.

**Alternatives considered**: proportional-only columns (unpredictable at 880-floor
widths); user-resizable columns (heavier machinery than FR-016 asks for).

## Decision: Settings header, re-check, copyable values, sign-out confirm (R5)

**Decision**: SettingsPage adopts the shared header card. A re-check action calls
`AuthApiClient.GetAuthMeAsync()` (existing `GET /api/auth/me`, already used at startup
`MainWindow.xaml.cs:94-95`), updates `WpfConnectionState` (made observable so the
status row re-renders), and shows in-control busy; the never-used `RetryAction`
hook (`App.xaml.cs:267`) is left untouched. Workstation URL and signed-in-as rows gain
copy buttons (notification seam). Sign-out (`SettingsPage.xaml.cs:55-60`) gains an
`IDialogService.ConfirmAsync` gate before the token store is cleared.

**Rationale**: The connection status today is a one-shot startup-config error snapshot
(`App.xaml.cs:256-268`) — a re-check needs a live call, and `GetAuthMeAsync` is the
existing cheapest authenticated probe whose failure strings
("cannot reach AI workstation: …") the status presentation already pattern-matches.

**Alternatives considered**: a dedicated health endpoint (new API surface — violates
FR-023); re-running the config resolver (tests the config file, not the connection).

## Decision: Dashboard header adoption with a post-header identity row (R6, Q1)

**Decision**: DashboardPage wraps its 027 compact header in the shared header-card
structure (title, supporting line, actions area with the frozen `DashboardRefreshButton`
and labeled quick actions); the profile card (frozen `DashboardProfileCard`) moves to a
post-header row where the library-summary line sits left and the profile card sits
right. The 027 content below (six metric cards, panels, empty states) is untouched.

**Rationale**: Spec clarification Q1 — the header joins the shared treatment while the
027 redesign below survives; the post-header row gives the profile card a stable, roomy
home without hiding identity behind an avatar chip.

**Alternatives considered**: avatar chip in the header actions area (Q3-wireframe C —
rejected in Q1); moving the profile to Settings (breaks the signed-in-context
convention 027 preserved).

## Decision: Spacing tokens as shared Thickness resources (R6)

**Decision**: App.xaml gains shared `Thickness` resources on the documented 4-px scale
(page padding, header-card padding `16,12`, card paddings, empty-state body margin
`0,12,0,0`) plus a `PageHeaderCard` style for the `ui:Card` header; affected surfaces
reference them instead of inline literals. Chat bubble widths and dialog button
minimum widths normalize to the same scale.

**Rationale**: FR-020/SC-011. No Thickness resource exists today — every padding is an
inline literal and five card paddings, three dialog button widths, and two empty-state
margins already drift; resources are the lightest enforcement the XAML layer has.

**Alternatives considered**: a shared `PageHeader` component (header actions vary too
much per page to encapsulate without a property explosion); documentation-only (no
enforcement — the drift recurs).

## Decision: Chat auto-scroll + copy feedback + clear conversation (R3)

**Decision**: ChatControl scrolls the messages `ListBox` to the newest item on the
collection change it already observes and marshals (`ChatControl.xaml.cs:86-97`,
`ScrollIntoView`); copy buttons (`ChatControl.xaml.cs:120-129`,
QueryDetail prompt/answer/citation) confirm via the notification seam; a new
`[RelayCommand] ClearConversationCommand` on `QueryViewModel` (clears `Messages` and the
`ConversationStore` messages) surfaces as a header action on QueryPage.

**Rationale**: The control already subscribes to collection changes for the empty-state
toggle — auto-scroll is the same hook plus one call. The clear command is the minimum
VM wiring the affordance needs (spec Assumptions allow it); the store's `ReplaceAll`
bulk-clear already exists.

**Alternatives considered**: scrolling via `ScrollViewer.ScrollToEnd` on the ListBox's
internal viewer (brittle template lookup; `ScrollIntoView` is public API); clear via
page re-navigation (leaves the persisted conversation intact — misleading).

## Decision: Role-aware empty state and single-language copy (R6)

**Decision**: The Library empty state binds its hint to the viewer's role
(`LibraryViewModel` exposes admin visibility already used by the Upload button):
non-admins get guidance they can act on, not "Upload a document". QueryPage's
suggestion-chip header (currently bilingual Greek+English) becomes single-language
plain copy.

**Rationale**: FR-019/FR-021 (SC-010): one empty state points non-admins at an
admin-only action; one header mixes two languages.

**Alternatives considered**: hiding the empty state for non-admins (worse — a blank
table with no explanation).

## Verification decisions

- Unit tests first for every Core seam: `INotificationService` (in-memory double records
  shown notifications), `AskNavigationState` (set → read → clear), `ClearConversationCommand`
  (clears VM `Messages` and the store), per-download busy gating.
- Run the 023 static audits (no hard-coded colours, no `Opacity=`, no ad-hoc
  `ui:TextBlock FontSize`, valid `SymbolRegular`/`ThemeResource` keys, frozen IDs) over
  every touched file, extended with: zero `MessageBox.Show` in the client, zero
  `TextTrimming` on status text, row-action buttons ≥44 DIPs.
- Walk every page with `winapp ui` at a wide size and 800×600, cycling Light/Dark/High
  Contrast: header parity, status+retry on forced load failure, in-control busy,
  snackbar confirmations (copy/download/sign-out), chat auto-scroll over 10+ exchanges,
  reliable ask-again, table collapse, login Enter-submit, Settings re-check, keyboard
  reachability of every new affordance.
- Full regression: existing unit/contract/offline-integration suites pass with zero
  assertion changes; CSharpier clean (XAML included).
