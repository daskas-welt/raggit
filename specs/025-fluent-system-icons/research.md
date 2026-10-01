# Research: Fluent System Icons Across the Client

**Feature**: `025-fluent-system-icons` | **Date**: 2026-09-30 | **Plan**: [plan.md](plan.md)

## Decision: The icon set — the Fluent system icons the library already ships

**Rationale**: The client already renders every one of its icons through the component library's
`SymbolIcon` (`Symbol` + `Filled`), whose `SymbolRegular` enum is the Fluent system icon set. The
navigation, dashboard tiles, empty states, status chip, pager and several action buttons all use it
today, and `023` already made "every referenced name is a valid `SymbolRegular` member" a contract with
a reflection audit. Continuing with that set means no new font, no glyph codepoints, no new package, and
no second vocabulary to reconcile.

**Alternatives considered**: the operating system's Segoe Fluent Icons through `FontIcon` with raw
glyph codepoints — rejected, it duplicates the font the library already embeds and reintroduces the
plain-text-glyph class of defect (unvalidated codepoint, no compile-time name, High Contrast surprises)
that `012` and `023` removed; a third-party icon package — rejected, it adds a dependency, a licence and
a bundle for icons the client already has.

## Decision: The new concept → glyph vocabulary

**Rationale**: `023` fixed one glyph per concept for the surfaces that already carried icons. This
feature adds the concepts the text-only surfaces need. Each proposed name was verified as a member of
`Wpf.Ui.Controls.SymbolRegular` by reflection against `Wpf.Ui.dll` **before** being put in the spec's
plan (see the verification method below); the repository's standing trap is a name that compiles but
throws `XamlParseException` at load.

| Concept | Glyph | Surfaces (this feature) |
|---------|-------|-------------------------|
| Sign in | `ArrowEnterLeft24` | `LoginPage` primary button |
| Sign out | `SignOut24` | `SettingsPage` sign-out button (already the row glyph) |
| Create person | `PersonAdd24` | `AdminUsersPage` Create Person form |
| Change role | `PersonEdit24` | `AdminUsersPage` per-row role button |
| Reset password | `KeyReset24` | `AdminUsersPage` Reset Password form |
| Activate person | `Checkmark24` | `AdminUsersPage` per-row active toggle (inactive → Activate) |
| Deactivate person | `Dismiss24` | `AdminUsersPage` per-row active toggle (active → Deactivate) |
| Person locked | `LockClosed24` | `AdminUsersPage` lock-state cell |
| Person not locked | `LockOpen24` | `AdminUsersPage` lock-state cell |
| Retry | `ArrowClockwise24` | `LibraryPage` retry button — shares the reload concept with Refresh |
| Load more | `ArrowDown24` | `HistoryPage`, `DocumentsMinePage` |
| Cancel | `Dismiss24` | `UploadDialog` footer |
| Close | `Dismiss24` | `UploadDialog` footer — shares the dismiss concept with Cancel |
| Ask again | `ArrowRepeatAll24` | `HistoryPage` row, `QueryDetailPage` header |
| Back | `ArrowLeft24` | `QueryDetailPage` header |
| View | `Eye24` | `HistoryPage` row |
| Sources | `TextQuote24` | `ChatControl` citations disclosure (clarified 2026-09-30) |
| Reachable | `PlugConnected24` | `SettingsPage` connection-status value |
| Unreachable | `PlugDisconnected24` | `SettingsPage` connection-status value |
| Message: informational | `Info24` | admin status surface, footer status text |
| Message: success | `CheckmarkCircle24` | admin status surface |
| Message: warning | `Warning24` | admin status surface, upload InfoBar (auto today) |
| Message: error | `ErrorCircle24` | admin status surface, footer error text on Library/History/My Docs/Query Detail |

**Reused unchanged from `023`** (no change, verify only): Dashboard `Home24` · Library `Library24` ·
Ask `Chat24` · History `History24` · My Docs `Document24` · Admin `People24` · Settings `Settings24` ·
Refresh `ArrowClockwise24` · Upload `ArrowUpload24` · Browse `FolderOpen24` · Download `ArrowDownload24` ·
Delete `Delete24` · Copy `Copy24` · Send `Send24` · Pager `Chevron*20` · status chip
`Circle24`/`ArrowSyncCircle24`/`CheckmarkCircle24`/`ErrorCircle24`.

**Naming notes and rejected alternatives**:

- **Sign in**: the enum has `SignOut24` but no `SignIn24`; `ArrowEnterLeft24` is the available "enter"
  glyph at the control size. `ArrowRight24` was considered — rejected as a generic "go" arrow that
  carries no entry meaning.
- **Reset password**: `KeyReset24` exists and names the operation exactly; `Password24` (a password
  field glyph) and `Key24` were rejected as less specific.
- **Retry / Refresh**: both are "reload"; `023` already fixes `ArrowClockwise24` for Refresh. A second
  near-duplicate (`ArrowCounterclockwise24`, `ArrowRotateClockwise24`) was rejected so the two surfaces
  stay one concept.
- **Cancel / Close**: both dismiss the dialog; one `Dismiss24` glyph keeps the dismiss concept single.
- **Ask again**: `ArrowRepeatAll24` reads "run this again"; `Chat24` was rejected because it would
  collide with the Ask destination concept.
- **Sources**: `TextQuote24` reads "citation"; `BookOpen24` was rejected because the open book is the
  library/reading family already occupied by `Library24`/`BookInformation24`.
- **View**: `Eye24` reads "view"; `Open24` was rejected as it reads as opening a file and collides with
  the browse family.
- **Load more**: `ArrowDown24`; `ChevronDown24` (expand/collapse) and `MoreHorizontal24` (overflow menu)
  were rejected as controls, not content loading.
- **Message severity**: reuses the status-chip shapes (`CheckmarkCircle24`, `ErrorCircle24`) plus
  `Info24`/`Warning24`, so success and error mean the same thing on every surface.

## Decision: Where icons are appropriate — the bounded boundary

**Rationale**: The spec's FR-002/FR-009 and the 2026-09-30 clarification bound "where appropriate" to
actions and states. Icons are added where a standard symbol aids recognition, and deliberately **not**
where they would be redundant or misleading.

**In scope**: action controls whose meaning has an agreed symbol (the table above plus the reused
`023` concepts); state indicators (document lifecycle, person enabled/locked, connection reachability,
message severity); and the single "Sources" disclosure.

**Explicitly out of scope** (FR-009): the Ask page's repeated "did you mean?" person suggestion chips
(one identical icon per chip is repetition without meaning — clarified 2026-09-30); table column
headers; numeric page buttons; the pager ellipsis; free-text fields (username, password, new-user
form); theme radio options; and plain content text.

**Alternatives considered**: decorating every interactive element uniformly — rejected by the
clarification and by `023`'s one-glyph-per-concept rule, which is about meaning, not coverage; adding
icons only to icon-only controls — rejected, it would leave the text-only action buttons (the bulk of
the gap) unchanged.

## Decision: State glyphs, without reverting the `016` labelled-admin decision

**Rationale**: FR-004 requires every stateful surface to convey state with a glyph as well as colour.
Three of the four are already compliant or trivial: the document status chip already switches glyph and
colour per state; the settings connection value gains an inline reachability glyph driven by the
existing `WpfConnectionState.IsUnavailable`; and the plain status/error text surfaces gain a severity
glyph (the admin view model already exposes `StatusSeverity`).

The admin people list is the delicate one. `016` deliberately replaced the old glyph-only active toggle
with **clearly labelled** text buttons, and the lock column is display-only text. This feature keeps
both decisions: the active toggle becomes **icon + label** (`Activate` with `Checkmark24`, `Deactivate`
with `Dismiss24`) so the action is recognisable and the two states are distinct by glyph as well as
word, and the lock cell becomes **icon + label** (`LockClosed24` "Locked", `LockOpen24` "Not locked")
with the existing per-row accessible names intact. No control becomes icon-only, and lock state stays
display-only (no new unlock action).

**Alternatives considered**: reverting the toggles to glyph-only — rejected, it would reverse `016`
FR-003 and remove the accessible labels; a separate state column beside the action buttons — rejected,
it changes the table's column model for no user benefit; an `InfoBar` for the admin status surface —
rejected, it would replace the frozen `AdminStatusBar` surface and its descriptor for a purely visual
gain; a leading state chip before the active button — rejected as duplication of the button's own
verb.

## Decision: Sizes — the documented `023` scale, unchanged

**Rationale**: `023` owns the scale (inline 16 · control 20 · row 24 · feature 40) and records that a
button-hosted or row glyph measures 24 in practice, the pager is the documented 20 fallback, and empty
states use the shared `EmptyStateGlyph` (40). This feature adds no size: every new glyph is a `*24`
button/row/list glyph, drawn at the library's control size, and every icon-only button keeps its current
36×36 or 44×44 hit target (the glyph inside does not change the target).

**Alternatives considered**: a new inline size for the connection value or the message glyphs —
rejected; they sit in row/body contexts and use the row step, and adding a step would break `023`'s
single-scale contract; explicit `FontSize` on each new glyph — rejected, the library's control style
sizes them and `023`'s D1/D3 audits treat ad-hoc sizes as drift.

## Decision: Accessibility — names and tooltips preserved, labels retained

**Rationale**: FR-007 and FR-012. Icon-only controls (pager arrows, copy, delete, send, the icon-only
header actions, the upload dialog's remove button) already carry `AutomationProperties.Name` and a
tooltip; new icon-only states keep that pattern, and every glyph paired with a label leaves the label's
accessible name untouched. A glyph is decoration, never the accessible name.

**Alternatives considered**: relying on the glyph's text for the accessible name — rejected, the library
exposes a raw glyph character; auto-generating names from the icon — rejected, the name is the action,
not the picture.

## Decision: How the vocabulary is documented (FR-010)

**Rationale**: The single source of truth is
`specs/023-design-system-refinement/design-system.md`, which already holds the icon scale, the state
conventions and the concept list, and which `024` extended in place for the brand colour. This feature
adds the new concept→glyph entries there rather than creating a competing document, so a later surface
finds one list.

**Alternatives considered**: a new `025` design document — rejected, it would split the vocabulary
across two owners and invite drift, the exact failure the one-vocabulary rule exists to prevent.

## Decision: How the icons are verified

**Rationale**: Presentation work in this repository is proven by static audits plus screenshots, not
unit tests (there is no client unit-test project). The verifiable pieces are:

1. **Name validity** — every `Symbol="…"` (XAML) and `SymbolRegular.*` (C#) name is a member of
   `SymbolRegular`, checked by reflection under PowerShell 7 (the `023` D3 pattern). This is the check
   that catches the "compiles, throws at load" class.
2. **Token discipline** — the `023` D2 checks (no hard-coded colour, no `Opacity=`) still pass over the
   touched files, so new glyph colour comes from theme roles.
3. **Coverage** — a grep-level inventory proves each listed action surface references its agreed glyph,
   and each concept appears with one glyph (no second symbol for refresh, dismiss, etc.).
4. **Behaviour freeze** — the `023` D7 check: every frozen automation identifier is still present.
5. **Rendered result** — `winapp ui` screenshots across Light/Dark/High Contrast and at 800×600, since
   static checks cannot see clipping, contrast or wrong theming.

**Verification method used for this plan** (PowerShell 7, from the repo root):

```powershell
$dll = Join-Path $env:USERPROFILE '.nuget\packages\wpf-ui\4.3.0\lib\net10.0-windows7.0\Wpf.Ui.dll'
$names = [Enum]::GetNames(([Reflection.Assembly]::LoadFrom($dll)).GetType('Wpf.Ui.Controls.SymbolRegular'))
# every name in the vocabulary table above was checked with: $names -contains '<Name>'
```

All names in this document and in the spec's plan returned present, with two exceptions that were
dropped from consideration: `DoorArrowRight24` (only `16/20/28/32` exist) and a bare `Wifi20` (only the
numbered `Wifi1..4 20` and the state variants exist).

**Alternatives considered**: a unit test over the XAML — rejected, the client has no unit-test project
and parsing XAML in a server test project is the wrong home; screenshot-only verification — rejected, a
picture cannot prove a name is valid or that a glyph is themed.
