# Research: Admin People Management Redesign

**Feature**: `026-admin-people-redesign` | **Date**: 2026-10-01 | **Plan**: [plan.md](plan.md)

## Decision: Master-detail layout, stacked responsively

**Decision**: Keep the people list as the primary pane and display the selected account in a detail
panel beside it. At content widths ≤720 DIPs, stack the detail panel below the list.

**Rationale**: Admins select a person before performing sensitive operations; persistent details make
the target visible. Stacking preserves the same workflow at narrow widths without hiding the panel.

**Alternatives considered**: full-width list with every action in dialogs (loses context); permanently
narrow side panel (compresses a window whose minimum outer width is 800); a separate details route
(adds navigation hops and can lose list context).

## Decision: WPF collection view for local list operations

**Decision**: Build search, role/active filters, and sort over the currently loaded observable account
collection using WPF's collection-view mechanism in the page layer. Do not persist view state or change
the GET users request.

**Rationale**: Current API returns the complete small single-tenant people directory, and the requested
baseline is 100 accounts. Keeping this state in the WPF view meets the approved boundary and avoids
changing shared user behavior for presentation-only operations.

**Alternatives considered**: server-side query parameters (unnecessary API scope for this data size);
shared ViewModel filter state (adds cross-client behavior contrary to the decision); manually rebuild a
filtered collection on each keypress (duplicates collection-view behavior).

## Decision: Confirmation-gated explicit role selection

**Decision**: Show the role in a row ComboBox and use a command that accepts the requested target role.
On selection, save the old role, show the existing confirmation pattern, and submit only after confirm;
cancel/failure restores the old display.

**Rationale**: Explicit role choices are easier to understand than a button that toggles to the other
role. A target-role command is necessary because a confirmation may be canceled and the ViewModel must
not infer the target by toggling a prematurely changed row object.

**Alternatives considered**: direct two-way role binding (mutates local state before confirmation);
immediate save (no security-sensitive confirmation); keep the role-flip button (less explicit).

## Decision: Modal forms reuse existing ViewModel operations

**Decision**: Create and Reset Password are separate owner-modal WPF windows using existing WPF-UI
theme resources and controls. Keep form state and validation in `AdminUsersViewModel`; close only after
success, and preserve fields after failure.

**Rationale**: The page no longer needs persistent form columns, and a modal makes it clear which task
is in progress. This follows the existing owner-modal UploadDialog pattern and avoids a new dialog
framework or duplicate account logic.

**Alternatives considered**: persistent forms below the list (the layout being replaced); separate
forms in the detail pane (competes with account facts and list width); duplicate dialog-specific
ViewModels and API calls (duplicates behavior/validation).

## Decision: Distinguish state from actions

**Decision**: Show Active/Disabled as a status chip, Locked only when true, and a separate
Activate/Deactivate action. Keep lockout display-only. The row role selector is a separate role-action
control, not the status display.

**Rationale**: Current “Deactivate” text in the Active column describes the next action, not the current
state. Separating state and action improves scanning and avoids implying that lockout can be changed.

**Alternatives considered**: active toggle switch (compresses a confirmed security-sensitive operation
into an unconfirmed toggle); “Not locked” on every row (adds repetitive noise); color-only status
(inaccessible and ambiguous in High Contrast).

## Decision: Last-active-admin guard inside an atomic repository operation

**Decision**: Add a patch-specific repository operation that opens a SQLite write transaction, rereads
the persisted target, checks whether it is an active Admin being demoted/deactivated, counts other
active Admins, and updates the requested patch fields before committing. Return an explicit outcome for
Updated, NotFound, or LastActiveAdmin. Map LastActiveAdmin to HTTP 409 with the standard Error response.

**Rationale**: The current controller's separate read and write can race. A transaction that acquires a
write lock before checking ensures two overlapping updates cannot both observe the same last admin and
remove it. Keeping the operation in the repository preserves the transaction boundary and avoids
process-local synchronization.

**Alternatives considered**: client-side count/disable (bypassable and stale); controller count then
`UpdateAsync` (check/write race); in-process semaphore (not shared across API processes); adding a
persisted count/flag (derived state and schema churn).

**Invariant**: “Active Admin” is `Role=Admin && IsActive=true`; a temporary lockout does not count as
deactivation. The UI warns on self-change, but the API only refuses removal of the final active Admin.

## Decision: Contract and version

**Decision**: Add a 409 response to PATCH `/api/users/{id}` in the existing identity OpenAPI contract
and advance it to 1.4.0. Align the Workstation API version metadata to 1.4.0.

**Rationale**: The request and existing DTO remain unchanged; the new conflict outcome is explicitly
documented as an additive MINOR contract update. Matching the API metadata keeps `/health` and the
OpenAPI contract coherent.

**Alternatives considered**: leave the contract version at 1.3.0 (fails to signal a new documented
response); make a major version (unnecessary because no request schema or existing successful response
is removed).

## Verification decisions

- Write repository/unit and API contract/integration tests before guard implementation.
- Include simultaneous demote/deactivate attempts against two active admins; exactly one operation may
  remove an admin while the final active admin remains.
- Verify view filters and sort use the loaded collection and leave account data unchanged.
- Use the `winapp ui` page walkthrough for dialogs, keyboard, accessibility IDs, theme variants and
  narrow layout; preserve all 11 frozen Admin IDs.
