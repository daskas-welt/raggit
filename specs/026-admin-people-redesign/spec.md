# Feature Specification: Admin People Management Redesign

**Feature Branch**: `026-admin-people-redesign`

**Created**: 2026-10-01

**Status**: Draft

**Input**: User-approved redesign of AdminUsersPage: master-detail layout, searchable/filterable/sortable people list, role/status controls, create/reset dialogs, responsive details, and last-active-admin protection.

**Constitution**: v1.3.0 — presentation changes reuse the existing desktop client; the only service behavior addition is a safety guard on existing user updates. No tenant, AI, retrieval, or query behavior changes.

## Clarifications

### Session 2026-10-01

- Q: How should the page be laid out? → A: People list on the left and selected-person details on the right; details stack below the list at narrow content widths.
- Q: How should role changes work? → A: A role dropdown in each row; choosing a different role requires confirmation before applying.
- Q: How should active and lock state read? → A: Show an Active/Disabled status chip, show Locked only when locked, and provide a separate action to change active state. Lockout remains display-only.
- Q: Where should create and reset forms live? → A: In dialogs opened from Add person and Reset password actions.
- Q: How should large directories be navigated? → A: Search, role and active-status filters, and sortable columns; these are transient view state and are not persisted.
- Q: How should last-admin and self actions be protected? → A: Confirm self-demotion/deactivation in the UI and enforce the last-active-admin invariant on the workstation API. A refusal is shown to the admin.
- Q: How should the redesign be delivered? → A: A new feature that supersedes the three-section layout in `016-admin-redesign`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Find and inspect a person (Priority: P1)

An admin opens People Management and sees a readable, virtualized people list beside a detail panel for the selected person. The admin can quickly locate an account by username or display name, narrow the list by role or active state, and sort it by a visible column. At narrow content widths the detail panel moves below the list instead of squeezing the rows.

**Why this priority**: Finding the right account is the start of every admin task; the current fixed-width, scroll-disabled list truncates identities and wastes space.

**Independent Test**: Seed at least 100 accounts, search for a known username/display name, apply role and active-state filters, sort a column, and select a result. Verify the detail panel identifies that same person and that the list remains usable at 800×600.

**Acceptance Scenarios**:

1. **Given** a people directory, **When** the admin types part of a username or display name, **Then** only matching rows remain visible.
2. **Given** the directory, **When** the admin selects a role or active-state filter, **Then** only matching accounts remain visible; clearing filters restores the full list.
3. **Given** a visible sortable column, **When** the admin activates its header, **Then** the visible rows sort by that field and the sort direction is indicated.
4. **Given** a selected row, **When** it is selected, **Then** the detail panel shows its username, display name, role, active/disabled state, lock state, creation time, and last sign-in when available.
5. **Given** page content at or below 720 DIPs, **When** the layout adapts, **Then** the detail panel stacks below the list and both panes remain reachable without clipping.
6. **Given** no users or no matching users, **When** the list is empty, **Then** a distinct empty state explains whether the directory is empty or the filters found no matches and offers a next step.

---

### User Story 2 - Change a person's role or active state safely (Priority: P1)

An admin sees current account state directly in the list: role, an Active/Disabled chip, and a Locked indicator only when the account is locked out. Role is changed through a row dropdown with confirmation. A separate row/detail action activates or deactivates the account with confirmation. A self-targeted change clearly warns that the admin may lose access.

**Why this priority**: Role and active state control access to the workstation and must be legible before an admin changes them.

**Independent Test**: Change a non-final user's role and active state, cancel each confirmation once, then confirm each change. Verify canceled changes leave the account unchanged, confirmed changes update the row and detail panel, and a self-targeted action explains its access impact.

**Acceptance Scenarios**:

1. **Given** a user row, **When** the admin opens the role dropdown, **Then** the current role is selected and only supported roles are offered.
2. **Given** a different role is selected, **When** the admin cancels confirmation, **Then** the previous role remains selected and no update is sent.
3. **Given** a different role is selected, **When** the admin confirms, **Then** the role is updated and the list/detail reflect the returned account.
4. **Given** an active or disabled account, **When** viewed, **Then** its status chip says Active or Disabled; a Locked indicator is present only for locked accounts.
5. **Given** an active-state action, **When** the admin cancels confirmation, **Then** the account remains unchanged; when confirmed, the action and chip reflect the updated state.
6. **Given** the admin targets their own account, **When** they initiate demotion or deactivation, **Then** confirmation warns that their access/session may end before the change is submitted.
7. **Given** an account's lockout state, **When** the row or detail panel is viewed, **Then** lockout is display-only; the page provides no unlock action.

---

### User Story 3 - Create a person in a focused dialog (Priority: P1)

An admin chooses Add person and receives a focused form dialog for username, display name, role, and initial password. Validation and server errors explain what to fix without discarding entered values; successful creation closes the dialog and shows the new account in the list.

**Why this priority**: Account creation is a core admin task; moving it out of the persistent page reduces form clutter while keeping the flow direct.

**Independent Test**: Open Add person, create an account, then repeat with missing and duplicate values. Verify success adds the account, failures retain the draft and give actionable feedback, and Cancel leaves the directory unchanged.

**Acceptance Scenarios**:

1. **Given** the Admin page, **When** Add person is activated, **Then** a dialog presents username, display name, role, and initial password with visible labels.
2. **Given** required values are missing or invalid, **When** Create is submitted, **Then** validation identifies the missing/invalid field and preserves all entered values.
3. **Given** a duplicate username or server failure, **When** creation is attempted, **Then** a plain-language error is shown and the dialog draft is retained.
4. **Given** valid account details, **When** creation succeeds, **Then** the dialog closes, the new account appears in the list, and a success outcome is announced.
5. **Given** the create dialog, **When** Cancel is activated, **Then** no account is created.

---

### User Story 4 - Reset a person's password in a targeted dialog (Priority: P1)

An admin starts Reset password from a row or selected-person detail panel. The dialog visibly names the target, accepts a new password and the existing must-change option, and keeps the admin from resetting the wrong account.

**Why this priority**: Password resets are security-sensitive; the target identity and outcome must be unmistakable.

**Independent Test**: Open reset for a chosen account, verify its identity, submit an invalid and then a valid password, and verify the existing confirmation and must-change behavior are preserved.

**Acceptance Scenarios**:

1. **Given** an account row or selected-person detail, **When** Reset password is activated, **Then** a dialog identifies the target account and shows the new-password and must-change fields.
2. **Given** a dialog for one account, **When** the admin confirms reset, **Then** that account—not another selected or focused account—is updated.
3. **Given** a missing/invalid password or server failure, **When** reset is attempted, **Then** the dialog remains open with an actionable message and the must-change choice intact.
4. **Given** reset succeeds, **When** the result returns, **Then** the dialog closes and a success outcome identifies the account.
5. **Given** the reset dialog, **When** Cancel is activated, **Then** no password change is submitted.

---

### User Story 5 - Preserve the last active admin (Priority: P1)

The workstation refuses a role or active-state update that would leave the company with no active Admin account. The admin sees the server's actionable reason and can add/promote another admin before retrying.

**Why this priority**: Removing the final active admin can permanently prevent normal account administration.

**Independent Test**: With exactly one active Admin, attempt to demote and deactivate that account and verify both are refused without changing it. With two active Admins, verify one can be demoted or deactivated while the other remains active.

**Acceptance Scenarios**:

1. **Given** exactly one account with Admin role and active state, **When** a request would demote or deactivate it, **Then** the workstation refuses the update with a conflict outcome and leaves the account unchanged.
2. **Given** two or more active Admin accounts, **When** one is demoted or deactivated, **Then** the update succeeds and at least one active Admin remains.
3. **Given** the server refuses an update, **When** the Admin page receives the response, **Then** it displays the server's plain-language reason in the status surface and preserves the actual account state.
4. **Given** an update that changes only a display name or changes a non-admin account, **When** it is submitted, **Then** the last-admin safeguard does not block it.

---

### User Story 6 - Operate the page accessibly at supported sizes and themes (Priority: P2)

A keyboard, touch, or screen-reader user can reach the list, filters, detail actions, and dialogs in a logical order. Light, Dark, and High Contrast themes preserve readable state and focus cues; the layout remains usable at the minimum supported window size.

**Why this priority**: The redesigned information density must remain operable for all admins, not only at a wide desktop size.

**Independent Test**: Complete list selection, filtering, role change, create, reset, and cancel flows using keyboard and a screen reader; inspect Light/Dark/High Contrast at 800×600 and a wider desktop size.

**Acceptance Scenarios**:

1. **Given** keyboard-only use, **When** moving through the page and dialogs, **Then** every action and field is reachable in logical order with visible focus and dialog dismissal works.
2. **Given** screen-reader use, **When** navigating users and actions, **Then** names identify the account and action, statuses are announced, and dialogs identify their target.
3. **Given** Light, Dark, or High Contrast, **When** the page renders, **Then** state, focus, selection, and status remain distinguishable without relying on color alone.
4. **Given** an 800×600 window, **When** the Admin page is used, **Then** list, details, filters, and dialogs remain reachable without clipping.

### Edge Cases

- A filter combination yields no results: show a filtered-empty state and a clear way to clear filters.
- No account is selected: detail panel gives a selection hint; reset is unavailable until a target is selected.
- A row is filtered out while selected: clear selection and show the selection hint rather than stale details.
- A selected account is changed by another admin: refresh replaces the row from server truth; failed updates never optimistically leave stale values.
- Self-demotion/deactivation succeeds when another active Admin remains: warn before submission and surface the normal session-expiry outcome if the current session ends.
- A temporarily locked account remains an active Admin for the last-admin count; lockout is distinct from deactivation.
- Create/reset dialogs are opened while an operation is already busy: prevent duplicate submissions.
- User names and display names are long: truncate in rows, show the full value in details, and do not displace row actions.
- At narrow content widths, details stack below the list and remain reachable by scrolling.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The Admin page MUST present a people list and selected-person detail panel; at content widths of 720 DIPs or less, the detail panel MUST stack below the list.
- **FR-002**: The people list MUST show username/display name, role, Active/Disabled state, and a Locked indicator only when locked; it MUST remain virtualized for at least 100 accounts.
- **FR-003**: The page MUST provide username/display-name search, role and active-state filters, and sortable visible columns; these controls MUST affect only the current in-memory view and MUST NOT be persisted or sent as server filters.
- **FR-004**: Selecting a row MUST populate the detail panel with that same account's username, display name, role, active state, lock state, creation time, and last sign-in when available.
- **FR-005**: Each row MUST allow role selection from the supported roles; selecting a different role MUST require confirmation, and cancel MUST restore the prior role without sending an update.
- **FR-006**: Each row MUST display Active or Disabled as a status chip and provide a separate action to change active state; existing confirmation behavior MUST remain. Lock state MUST remain display-only.
- **FR-007**: Add person MUST open a dialog containing visible labels for username, display name, role, and initial password; existing server validation rules MUST remain unchanged, and failed submissions MUST preserve the draft.
- **FR-008**: Reset password MUST open a dialog that identifies its target account and supports the existing password and must-change fields; existing confirmation, validation, and server behavior MUST remain unchanged.
- **FR-009**: Self-demotion or self-deactivation MUST present a confirmation warning that the admin may lose access or be signed out.
- **FR-010**: The workstation MUST reject a PATCH that would remove the final account whose role is Admin and `IsActive` is true, whether by demotion or deactivation; it MUST return HTTP 409 with a user-safe actionable error and MUST leave the account unchanged.
- **FR-011**: The last-admin guard MUST NOT block display-name-only updates, changes to non-admin accounts, or a role/active change when another active Admin remains. A temporary lockout MUST NOT count as deactivation.
- **FR-012**: Every loading, success, validation, confirmation, and failure outcome MUST be visible and accessible; duplicate submissions MUST be prevented while the relevant operation is busy.
- **FR-013**: Existing Admin page automation IDs MUST be preserved when controls move; new interactive controls MUST receive stable automation IDs and accessible names.
- **FR-014**: All interactive elements MUST remain keyboard-operable with visible focus, target sizes of at least 44 DIPs, and legible Light/Dark/High Contrast treatments without hard-coded colors.
- **FR-015**: User DTOs, persisted user fields, auth/RBAC rules other than the last-active-admin safeguard, and all unrelated API behaviors MUST remain unchanged.
- **FR-016**: The OpenAPI contract MUST document the new 409 response for the user PATCH operation, and contract/integration tests MUST cover its refusal and allowed-update cases.

### Key Entities

- **User account**: Existing account identity and display name, role, active flag, lockout state, creation time, and optional last-sign-in time; no new persisted fields.
- **Directory view state**: Transient search text, role filter, active-state filter, sort field/direction, and selected account; not persisted and not part of the API.
- **Create-person draft**: Existing username, display name, role, and initial-password form values; retained when validation or server creation fails.
- **Password-reset draft**: Existing target account, new password, and must-change choice; scoped to the reset dialog and target.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In a directory of 100 accounts, an admin can find a known account by username or display name in 10 seconds or less using search.
- **SC-002**: 100% of role/state changes either complete after confirmation or leave server and displayed state unchanged after cancellation/refusal.
- **SC-003**: With exactly one active Admin, 100% of demotion/deactivation attempts are refused and the account remains active Admin; with two active Admins, an allowed change leaves at least one active Admin.
- **SC-004**: 100% of create/reset failures preserve the form draft and show an actionable outcome; successful actions show confirmation.
- **SC-005**: At 800×600 and a wide desktop size, every control remains reachable and no critical identity/action content is clipped; Light, Dark, and High Contrast walkthroughs pass.
- **SC-006**: All frozen automation IDs remain present; the solution builds and unit, contract, and offline integration suites pass with no unrelated assertion changes.

## Assumptions

- “Last active Admin” means an account with `Role == Admin` and `IsActive == true`; a temporary lockout does not remove it from this count.
- The UI warns but does not forbid an admin from changing their own account when another active Admin remains; the server guard only blocks removal of the final active Admin.
- Search, filters, sorting, and selection are view-only state over the current directory response; no persistence or API query changes are introduced.
- Role and active-state updates continue to use the existing PATCH operation; only its last-active-admin refusal is new.
- The existing user DTO fields are sufficient; this feature adds no stored user data and no new user-management operation.
- The minimum supported desktop window remains 800×600; the detail panel stacks when the page content width is at most 720 DIPs.

## Dependencies

- Existing local account, role, active-state, lockout, and reset-password behavior from `specs/004-identity/`.
- Existing WPF page, shared user-management view model, WPF-UI controls, and status/theme conventions.
- Existing Admin page automation-ID baseline and cross-client theme/accessibility rules.

## Out of Scope

- Unlocking accounts, deleting accounts, audit history, bulk operations, pagination/server-side search, or new user fields.
- Changes to authentication mechanisms, password policy, non-admin authorization, query/retrieval behavior, or multi-tenant behavior.
- Rewriting historical feature specifications to change the UI framework they originally described.
