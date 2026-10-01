# UI and API Contracts: Admin People Management Redesign

**Feature**: `026-admin-people-redesign` | **Date**: 2026-10-01 | **Plan**: [plan.md](../plan.md)

Observable contracts for the Admin surface and the only new API outcome. Existing user-management
operations and DTOs remain the same.

## U1 — Directory and selected-person detail

1. People list and detail panel are visible together at wide widths; detail stacks below the list at
   page content widths ≤720 DIPs.
2. The list shows identity, role, active state, and lock state; it supports 100+ accounts without
   unbounded layout or lost keyboard access.
3. Search matches username/display name; role and active filters and column sorting apply locally to
   the loaded collection and do not persist or call a server filter endpoint.
4. The detail panel always corresponds to the selected visible account. If filtering hides the selected
   row, selection clears and details/actions do not retain stale account data.
5. Empty directory and zero filter matches are distinguishable, with a clear way to recover.

**Prohibited**: detail actions targeting a filtered-out row; local account mutations caused by filter or
sort; a horizontally clipped identity/action area; more than one vertical scroll owner per pane.

## U2 — Role, status, and active action

1. A row role dropdown lists only Admin and Employee. A changed choice requires confirmation; cancel
   restores the original role and sends no update.
2. Active/Disabled is a state chip; a separate action changes the active state and confirms before
   submission.
3. Locked appears only when `LockedOut=true`. Lockout remains display-only; no unlock control exists.
4. The self-targeted role/active confirmation warns that the current admin may lose access or be signed
   out.
5. Server refusal never leaves an optimistic client-only state; the existing status surface reports the
   error returned by the workstation.

## U3 — Create and reset dialogs

1. Add person opens a modal form with visible labels for username, display name, role, and password.
2. Reset password opens a modal form bound to the intended account `Id`, visibly identifies the target,
   and contains the existing new-password and must-change controls.
3. Existing validation and confirmation rules are reused. Failures keep entered form state and show an
   actionable status; success refreshes the directory and closes the modal.
4. Frozen Admin automation IDs remain unchanged and move with the controls they identify:

   `RefreshUsersButton`, `AdminStatusBar`, `UsersList`, `NewUsernameBox`, `NewDisplayNameBox`,
   `NewRoleCombo`, `NewPasswordBox`, `CreateUserButton`, `ResetPasswordBox`,
   `ResetMustChangeCheck`, `ResetPasswordButton`.

5. New interactive elements receive stable automation IDs and meaningful accessible names.

## U4 — Accessibility, theme, and layout

1. All controls are keyboard reachable in a logical order with visible focus; modal cancel/dismiss
   behaves predictably.
2. Screen readers announce the account identity and purpose/target of row actions and dialog buttons.
3. Light, Dark, and High Contrast preserve focus/status meaning without color-only signaling or
   hard-coded colors.
4. Interactive targets are at least 44 DIPs; the 800×600 page has no clipped inaccessible controls.

## A1 — Last-active-admin PATCH response

For `PATCH /api/users/{id}`:

1. If the persisted target is `Role=Admin` and `IsActive=true`, and the requested patch would remove
   either condition while no other account is both Admin and active, return **409 Conflict** with the
   standard `{ "error": "…" }` body.
2. The refusal leaves the target account unchanged and the message tells the admin to keep or promote
   another active Admin first.
3. With at least one other active Admin, demotion/deactivation succeeds as before.
4. Display-name-only changes, non-admin changes, and other updates preserving the target's
   Admin+active state are unaffected.
5. Concurrent changes cannot remove all active Admins; check and update are atomic at persistence.

The OpenAPI source of truth is updated in
[`specs/004-identity/contracts/api.yaml`](../../../004-identity/contracts/api.yaml) to version 1.4.0.
