# Feature Specification: Admin Page Redesign

**Feature Branch**: `016-admin-redesign`

**Created**: 2026-09-18

**Status**: Draft

**Input**: User description: "redesign and implement Admin page - see [Image 1] - use winui-dev"

**Constitution**: v1.3.0 — no amendment needed (presentation-only redesign of the WinUI Admin page; Core/API/contracts/RBAC untouched).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Admin manages people from an organized page (Priority: P1) ★ MVP

An admin opens the Admin page and sees a well-organized people-management surface that uses the available window width: a readable user list (name, role, active state, lock state) with clear per-row actions, a create-person form with sensible field widths, and a reset-password form tied to the selected user. The page reads as a native settings surface in all themes.

**Why this priority**: The current page (Image 1) is functionally complete but visually broken — full-window-width cards and fields, cramped list, inconsistent controls. Layout and control correctness is the whole ask.

**Independent Test**: As admin, open Admin → verify the user list shows every account with role/active/locked; change a role, toggle active, create a new employee, reset a password — each succeeds with a clear outcome and the list reflects it.

**Acceptance Scenarios**:

1. **Given** an admin on the Admin page, **When** the page renders at 1280px width (representative desktop), **Then** content uses the available width with page padding (no large unused side margins) with the user list, create form, and reset form as distinct sections.
2. **Given** the user list, **When** it holds many accounts, **Then** it scrolls smoothly as a virtualized list with a visible empty state ("No users found") when there are none.
3. **Given** an admin, **When** they change a role, toggle active state, create a person, or reset a password, **Then** each action completes with a clear success or plain-language failure outcome and the list refreshes to match.
4. **Given** a destructive or security-sensitive action (e.g. role change, password reset), **When** it is triggered, **Then** the user confirms it before anything changes.

---

### User Story 2 - Admin always knows what happened (Priority: P2)

Errors and confirmations appear in a dedicated status surface (not a bare line of text that is easy to miss), problems name the specific field and remedy in the status message, and loading is visibly indicated while requests run.

**Why this priority**: Admin actions are security-sensitive; silent or easy-to-miss outcomes are a real risk.

**Independent Test**: Trigger a failure (e.g. create with a duplicate username, reset with no user selected) → a prominent message names the problem and the remedy; trigger success → a confirmation is shown; run refresh → a loading indicator is visible.

**Acceptance Scenarios**:

1. **Given** any admin action fails, **When** the error returns, **Then** a prominent status surface names the problem and what to do next.
2. **Given** any admin action succeeds, **When** it completes, **Then** a confirmation is shown (not silence).
3. **Given** requests in flight, **When** loading, **Then** the relevant section shows it is busy and duplicate submits are prevented.

---

### User Story 3 - Every admin can operate the page by keyboard and screen reader (Priority: P2)

A keyboard-only or screen-reader user reaches every row action, form field, and confirmation; row actions announce what they act on; the page renders legibly in Light, Dark, and High Contrast.

**Why this priority**: Same bar as the 011 redesign and the 015 upload refresh — overlay and management surfaces must meet it uniformly.

**Independent Test**: Full keyboard walkthrough (Tab/Enter/Esc) plus Narrator pass plus Light/Dark/HighContrast check, including a narrow (≤720px) window.

**Acceptance Scenarios**:

1. **Given** keyboard-only use, **When** tabbing the page, **Then** every control is reachable in logical order with visible focus and Esc dismisses any confirmation.
2. **Given** screen-reader use, **When** moving through rows and forms, **Then** each action announces which user it affects and outcomes are announced.

---

### Edge Cases

- No user selected + reset password: action cannot run silently — it explains a selection is required (or is disabled with a hint).
- Duplicate username on create: plain-language message naming the conflict; entered values preserved.
- Session expiry or connection loss mid-action: existing session-expired path applies; message explains connectivity.
- Last-admin safeguards (if any) are server decisions surfaced verbatim — the page never invents policy.
- Narrow window (≤720px): sections stack/reflow; no clipped unreachable controls.
- 100+ users: list stays smooth; no nested-scroll traps.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The page MUST present three distinct sections — user list, create person, reset password — using the available window width with comfortable page padding (no large unused side margins on wide windows).
- **FR-002**: The user list MUST show username, display name, role, active state, and lock state per row and MUST virtualize (smooth with 100+ users, proper empty state).
- **FR-003**: Each row MUST offer role change, active toggle, and lock-state visibility as clearly labeled, keyboard-operable controls that announce which user they affect.
- **FR-004**: The create form MUST validate inline (username, display name, role, initial password) and MUST preserve entered values when creation fails.
- **FR-005**: The reset form MUST operate on the visibly selected user, MUST require an explicit selection, and MUST support the must-change option.
- **FR-006**: Every outcome (success, failure with reason and next step, loading/busy) MUST appear in a dedicated status surface, not a bare text line.
- **FR-007**: Destructive and security-sensitive actions MUST ask for confirmation before executing.
- **FR-008**: Duplicate submits while busy MUST be prevented.
- **FR-009**: All interactive elements MUST meet ≥44px touch targets, full keyboard operability with visible focus, and screen-reader announcements.
- **FR-010**: All surfaces MUST render legibly in Light, Dark, and High Contrast with no hard-coded colors.
- **FR-011**: The page MUST remain admin-only (existing gating and server-side enforcement unchanged).
- **FR-012**: No behavior change to user management itself — same operations, same validation rules, same API and RBAC; only presentation is redesigned.

### Key Entities

- **User account**: Username, display name, role (Admin/Employee), active state, lock state — managed, never invented, by this page.
- **Create form**: New-account draft — username, display name, role, initial password; validated inline.
- **Reset form**: Selected user + new password + must-change flag.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Admins complete each core task (role change, active toggle, create, reset) unaided in under 2 minutes per task, 90% first-attempt success.
- **SC-002**: Zero silent outcomes across 20 action trials — every success and failure shows a visible confirmation or reason with next step.
- **SC-003**: 100-user list scrolls without jank and keyboard walkthrough plus Light/Dark/HighContrast check passes with zero unreachable or illegible elements.
- **SC-004**: All existing unit/contract/integration tests pass with zero assertion changes.

## Assumptions

- Operations stay exactly today's set: list/refresh, role change, active toggle, lock display, create person, reset password. No new admin capabilities (unlock action, delete user, audit log) unless the build uncovers them as already-existing behavior.
- Server API, contracts, RBAC, and validation rules are untouched; lock state remains display-only as today.
- Visual language follows the 011 redesign and the 015 upload refresh (native Windows 11 look, theme-aware surfaces, native status surfaces, icon buttons) — control choices resolved at build time with reference grounding.
- The existing admin ViewModel state and commands are reused and extended only as needed for presentation (same pattern as 015).

## Dependencies

- `004-identity`: per-person accounts and roles this page manages.
- `011-ui-redesign`: native theme/icon/status conventions this redesign must follow.
- `015-file-upload-ui`: `InfoBar`/icon/a11y patterns to stay consistent with.
