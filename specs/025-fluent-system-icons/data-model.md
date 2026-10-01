# Data Model: Fluent System Icons Across the Client

**Feature**: `025-fluent-system-icons` | **Date**: 2026-09-30 | **Plan**: [plan.md](plan.md)

No persisted data and no external contract. The "model" is the client's iconography vocabulary: the
concepts, the glyph each maps to, the size each is drawn at, the surfaces each appears on, and the
behaviour that must not move.

## Entity: Icon concept

Drives FR-001, FR-002, FR-003, SC-002, SC-007. One idea → exactly one Fluent glyph, everywhere.

| Field | Value |
|-------|-------|
| Concept | A product idea the client expresses visually (an action such as "refresh", a state such as "locked") |
| Glyph | A `SymbolRegular` member from the Fluent system icon set (list in [research.md](research.md)); **never** a raw character and never a name that is not in the enum |
| Variant | Regular at rest; `Filled="True"` only where `023` already uses it (active navigation, status chip) |
| Size step | One of the documented steps (see below) |
| Surfaces | Every place the concept appears; all of them use this one glyph |

**Validation rules**: no concept has two glyphs and no glyph serves two unrelated concepts (the
intentional pairings — Refresh/Retry, Cancel/Close, and the `023` status reuses — are documented as one
concept each); every referenced name exists in `SymbolRegular`.

## Entity: Icon size step

Drives FR-005, SC-006. Owned by `023`; this feature adds no step.

| Step | Size | Use | Realised by |
|------|------|-----|-------------|
| Inline | 16 | status chip glyph; navigation glyphs | explicit `FontSize="16"` (status chip) / the nav control's style |
| Control | 20 | pager chevrons | the documented fallback glyph family (`Chevron*20`) |
| Row | 24 | settings rows, list and row actions, button-hosted and tile chip glyphs | the component library's `SymbolIcon` control style (the client sets no size) |
| Feature | 40 | empty states | the shared `EmptyStateGlyph` style |

**Validation rules**: every new glyph is a `*24` action/row glyph drawn at the control style's size; no
ad-hoc `FontSize` is introduced; icon-only buttons keep their existing 36×36 or 44×44 hit targets.

## Entity: Action surface

Drives FR-002, FR-003, FR-011, FR-012, SC-001, SC-004, SC-008. The controls that gain — or keep — a
glyph. "Label" states whether the control's visible text is retained (it always is).

| Page / component | Control | Glyph | Label |
|------------------|---------|-------|-------|
| Login | Sign in | `ArrowEnterLeft24` | retained |
| Admin (People) | Refresh | `ArrowClockwise24` | retained |
| Admin (People) | per-row role | `PersonEdit24` | retained (role text) |
| Admin (People) | per-row active toggle | `Checkmark24` / `Dismiss24` | retained (Activate/Deactivate) |
| Admin (People) | lock-state cell | `LockClosed24` / `LockOpen24` | retained (Locked/Not locked) |
| Admin (People) | Create user | `PersonAdd24` | retained |
| Admin (People) | Reset password | `KeyReset24` | retained |
| Library | Refresh | `ArrowClockwise24` | icon-only today (unchanged) |
| Library | Upload | `ArrowUpload24` | icon-only today (unchanged) |
| Library | per-row Download / Delete | `ArrowDownload24` / `Delete24` | icon-only today (unchanged) |
| Library | Retry | `ArrowClockwise24` | retained |
| History | Refresh | `ArrowClockwise24` | retained |
| History | per-row View | `Eye24` | retained |
| History | per-row Ask again | `ArrowRepeatAll24` | retained |
| History | Load more | `ArrowDown24` | retained |
| My Documents | Refresh | `ArrowClockwise24` | retained |
| My Documents | Load more | `ArrowDown24` | retained |
| Ask | Send | `Send24` | icon-only today (unchanged) |
| Ask | suggestion chips | — (no icon) | text-only by clarification |
| Query Detail | Ask again / Back | `ArrowRepeatAll24` / `ArrowLeft24` | retained |
| Query Detail | Copy prompt / answer / citation | `Copy24` | retained |
| Chat | Copy message | `Copy24` | icon-only today (unchanged) |
| Chat | Sources disclosure | `TextQuote24` | retained |
| Upload dialog | Browse / Remove / drop zone | `FolderOpen24` / `Delete24` / `ArrowUpload24` | icon-only today (unchanged) |
| Upload dialog | Upload / Cancel / Close | `ArrowUpload24` / `Dismiss24` / `Dismiss24` | retained |
| Settings | Sign out | `SignOut24` | retained |
| Dashboard | header + tile actions | existing `Library24`/`Chat24`/… | icon-only today (unchanged) |

**Validation rules**: every listed control references its agreed glyph; no listed control is converted
from labelled to icon-only; every existing `AutomationProperties.AutomationId` on these surfaces is
preserved and any new interactive element receives a stable one.

## Entity: State indicator

Drives FR-004, SC-003, SC-005. Surfaces that report a lifecycle or status value; each pairs a glyph
with a colour role.

| Indicator | States | Glyph | Colour role |
|-----------|--------|-------|-------------|
| Document status chip | Uploading / Queued / Indexing · Ready · Failed · unknown | `ArrowSyncCircle24` · `CheckmarkCircle24` · `ErrorCircle24` · `Circle24` | accent decoration · success · critical · neutral (already implemented by `023`) |
| Person active | inactive → Activate · active → Deactivate | `Checkmark24` · `Dismiss24` | theme text tokens (button) |
| Person lock | locked · not locked | `LockClosed24` · `LockOpen24` | status (caution) · secondary text |
| Connection reachability | reachable · unavailable | `PlugConnected24` · `PlugDisconnected24` | success · critical |
| Message severity | Informational · Success · Warning · Error | `Info24` · `CheckmarkCircle24` · `Warning24` · `ErrorCircle24` | info · success · caution · critical |

**Validation rules**: no state is signalled by colour or word alone; a glyph never replaces the
accessible name; the accent **fill** token is never used as a glyph foreground (the `023` rule).

## Entity: Frozen behaviour

Drives FR-011, FR-012, FR-013, SC-008, SC-009.

| Field | Value |
|-------|-------|
| Automation identifiers | Every identifier frozen by `022`/`023` (22 IDs) and by later features remains present and unchanged |
| Labels | Every labelled control keeps its label; no control becomes icon-only |
| Suggestion chips | The Ask page's person suggestion chips stay text-only |
| Shared behaviour layer, workstation service, contracts | Untouched |
| Suites | Existing suites pass with zero assertion changes; the formatter gate stays clean |

## State Transitions

Not applicable as data — but the two rendered transitions this feature adds are the active toggle's
glyph swap (`Checkmark24` ⇄ `Dismiss24` with the verb) and the connection value's glyph swap
(`PlugConnected24` ⇄ `PlugDisconnected24` when `WpfConnectionState.IsUnavailable` changes). Both are
presentation-only re-renders of existing state; neither changes behaviour, commands or persisted data.
