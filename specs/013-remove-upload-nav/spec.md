# Feature Specification: Remove Upload Nav Item

**Feature Branch**: `013-remove-upload-nav`

**Created**: 2026-09-18

**Status**: Draft

**Input**: User description: "End-users should upload only from Document Library, there is no need for Upload item at left control panel"

**Constitution**: v1.3.0 — no amendment needed (navigation-only change in the WinUI client; Core/API/contracts untouched).

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Upload lives only in the Document Library (Priority: P1)

An admin looking at the left navigation pane sees Library, Ask, History, My Docs, and Admin — no Upload entry. Uploading happens exclusively through the Upload button in the Document Library header, which opens the upload dialog over the list.

**Why this priority**: Single entry point removes the redundant nav destination the user flagged; the Library header button already exists and is the natural context for uploads.

**Independent Test**: Launch as admin → nav shows five items, no Upload; press Upload in the Library header → dialog opens over the list; upload succeeds → new row appears. Repeat as employee → no upload entry anywhere.

**Acceptance Scenarios**:

1. **Given** a signed-in admin, **When** they open the nav pane, **Then** Library, Ask, History, My Docs, and Admin are listed and Upload is absent.
2. **Given** an admin on the Library, **When** they press Upload in the header, **Then** the upload dialog opens and a successful upload refreshes the list (unchanged behavior).
3. **Given** a signed-in employee, **When** they open the nav pane or the Library, **Then** no upload entry point is visible anywhere.

---

### Edge Cases

- Keyboard-only user: tab order skips no gaps where the Upload item was; pane remains fully traversable.
- Narrow window with Top pane mode: no Upload overflow item appears.
- This reverses the 011 T003 ruling (nav item opening the dialog); the 008-upload-sheet single-entry-point rule is restored as the authority.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The nav pane MUST NOT contain an Upload item for any role.
- **FR-002**: The Library header Upload button MUST remain the single upload entry point (admin-only, dialog over the list, list refresh after close — behavior unchanged).
- **FR-003**: Removing the item MUST NOT break pane selection state — the pane keeps reflecting the current page after any navigation.
- **FR-004**: All other nav items (Library, Ask, History, My Docs, Admin incl. admin gating) MUST behave exactly as before.

### Key Entities

- **Nav pane**: `MainWindow` `NavigationView` menu items (Upload removed).
- **Upload dialog**: `UploadDialog` + Library header button (unchanged host and flow).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Zero Upload entries in the nav pane in 100% of sessions (admin and employee).
- **SC-002**: Admin uploads end-to-end from the Library header with no navigation errors across repeated trials.
- **SC-003**: All existing unit/contract/integration tests pass with zero assertion changes.

## Assumptions

- The Library header Upload button (with its admin gating and post-close refresh) is kept as-is; only the nav item and its selection-handler branch are removed.
- 010 spec FR-004 (which listed Upload in the nav) is superseded by this spec for the nav surface.
- No API, contract, Core, or test changes.
