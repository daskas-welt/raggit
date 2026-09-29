# Feature Specification: Gallery-Style Design Language

**Feature Branch**: `022-gallery-design-language`

**Created**: 2026-09-29

**Status**: Draft

**Input**: User description: "Based on the WPF-UI Gallery app (https://github.com/lepoco/wpfui), enhance the app design: adopt the Gallery's design language across the desktop client shell, Dashboard, and Settings."

**Constitution**: v1.3.0 — no amendment needed (presentation-only; shared client behavior layer, APIs, and contracts untouched).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The app chrome feels like the component library's showcase (Priority: P1)

A user opens the desktop client and the window and navigation read as a first-party Fluent application: a modern translucent window backdrop with rounded corners, a navigation pane that can be collapsed with a visible toggle, pages that transition smoothly, and Settings pinned at the bottom.

**Why this priority**: The shell frames every screen; the Gallery's polish is most visible here, and these are the lowest-risk, highest-visibility changes. This is the MVP.

**Independent Test**: Launch the client; confirm the window backdrop, the pane toggle collapses/expands the pane, transitions animate on navigation, the destinations are listed in order, and Settings stays pinned at the bottom.

**Acceptance Scenarios**:

1. **Given** the client is open, **When** the user toggles the navigation pane, **Then** the pane collapses and expands and the content area reflows without clipping.
2. **Given** the navigation pane, **When** it renders, **Then** the destinations are listed in a single ordered group and Settings appears in a persistent footer position.
3. **Given** two different pages, **When** the user navigates between them, **Then** a smooth page transition is shown.

---

### User Story 2 - The Dashboard lands like the Gallery home (Priority: P1)

A user arriving at the Dashboard is greeted with a hero banner that carries the signed-in identity and context (welcome, role), and the primary destinations of the product are presented as clickable tiles, each with an icon, a title, and a one-line description, instead of plain buttons.

**Why this priority**: The Dashboard is the product's front door; a hero plus destination tiles is the single most identity-defining change and matches the Gallery's home surface.

**Independent Test**: Sign in, land on the Dashboard, and confirm the hero banner renders with theme-derived accent styling, and every primary destination is a tile that navigates correctly on a single click.

**Acceptance Scenarios**:

1. **Given** the Dashboard, **When** it renders, **Then** a hero banner shows the signed-in context using theme accent styling that adapts to the current theme.
2. **Given** the Dashboard, **When** the user views the destination tiles, **Then** each tile shows an icon, a title, and a one-line description, and activating it opens the corresponding destination.
3. **Given** the user is an administrator, **When** the Dashboard renders, **Then** the administrative destination is represented consistently with the other tiles.
4. **Given** an empty library, **When** the Dashboard renders, **Then** the tiles and hero still render correctly with no missing or clipped elements.

---

### User Story 3 - Settings read as labelled rows (Priority: P2)

A user opening Settings sees each preference as a labelled row — an icon, a title, a supporting description, and the control or value — grouped under section headings, rather than a stack of bare controls.

**Why this priority**: Settings is the densest configuration surface and benefits most from the row pattern, but it is not on the primary query path.

**Independent Test**: Open Settings; confirm every setting renders as a labelled row with icon, title, description, and its control/value, grouped under headings, and every existing control remains operable.

**Acceptance Scenarios**:

1. **Given** Settings, **When** it renders, **Then** each setting appears as a row conveying an icon, a title, a description, and the control or current value.
2. **Given** the appearance setting, **When** the user selects a theme, **Then** the application updates immediately and the selection persists for the session.
3. **Given** Settings, **When** the user operates it by keyboard only, **Then** every control in the rows is reachable with visible focus.

---

### Edge Cases

- Window resized to the minimum supported size (800×600): tiles reflow to fewer columns and nothing is clipped.
- High-contrast theme: hero text, tile text, and row descriptions remain legible; no fixed color becomes invisible.
- No accent color available from the system: the hero and tiles still render legibly using the library's fallback accent.
- Long user display name, role text, or workstation URL: content truncates gracefully instead of pushing controls out of view.
- A destination filtered out by the navigation search is still reachable after clearing the query.
- Administrator destination present or absent (employee account): the grouped navigation and Dashboard tiles remain coherent in both cases.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The application window MUST adopt the component library's modern Fluent window chrome (translucent backdrop and rounded corners) and open centered on screen.
- **FR-002**: The navigation pane MUST be collapsible via a visible toggle and MUST use a comfortable reading width when expanded.
- **FR-003**: Page navigation MUST present a smooth transition between destinations.
- ~~**FR-004**~~: *(Withdrawn during implementation review — the navigation search field was removed at the user's request; the pane lists destinations directly.)*
- **FR-005**: Navigation destinations MUST be listed in a single ordered group (no section headings), with the Settings destination remaining pinned in a persistent footer position.
- **FR-006**: The Dashboard MUST present a hero banner conveying the signed-in context (welcome message and role), styled with the active theme's accent so it adapts to light, dark, and high-contrast themes.
- **FR-007**: The Dashboard MUST present the primary destinations as tiles, each with an icon, a title, and a one-line description, and each MUST open its destination from a single activation.
- **FR-008**: The Settings screen MUST present each setting as a labelled row with an icon, a title, a supporting description, and its control or current value, grouped under section headings.
- **FR-009**: Existing automation identifiers on the affected surfaces MUST be preserved; every new interactive element MUST receive a stable automation identifier.
- **FR-010**: No hard-coded color values may remain on the affected surfaces; all color MUST derive from the component library's theme resources.
- **FR-011**: The affected surfaces MUST render legibly in light, dark, and high-contrast themes, including text over the accent-styled hero.
- **FR-012**: Icons used on the affected surfaces MUST come from the component library's bundled icon set and MUST reference valid icon names.
- **FR-013**: At the minimum supported window size (800×600), all primary content on the affected surfaces MUST remain reachable and unclipped.
- **FR-014**: No behavior change — the shared client behavior layer, the workstation API, and all contracts are read-only for this feature.
- **FR-015**: All primary actions on the affected surfaces MUST be keyboard reachable with a visible focus indicator.
- **FR-016**: Page content on the affected surfaces MUST be inset from the navigation pane and the window edges so it does not sit flush against them.

### Key Entities

Not applicable — this feature introduces no data, persistence, or API surface; it changes presentation only.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can reach any primary destination from the navigation pane with a single click, with the pane collapsed or expanded.
- **SC-002**: Every primary destination is reachable from the Dashboard in a single activation.
- **SC-003**: Zero hard-coded color values remain on the affected surfaces (baseline: measured color literals on the shell, Dashboard, and Settings before the change).
- **SC-004**: 100% of the automation identifiers present on the affected surfaces before the change are still present afterwards.
- **SC-005**: All primary content on the affected surfaces is reachable and unclipped at a window size of 800×600.
- **SC-006**: Every affected surface passes a light/dark/high-contrast visual check with no unreadable text.
- **SC-007**: The solution builds and all existing test suites pass with zero assertion changes, and the formatter gate is clean.

## Assumptions

- Scope is limited to three surfaces — the application shell (window chrome + navigation pane), the Dashboard, and the Settings page — not a page-by-page redesign of Library, Ask, History, My Documents, Admin, or Login.
- The component library already referenced by the desktop client remains the target; no competing UI library is introduced.
- The hero banner is built entirely from theme accent tokens; no bundled imagery or external assets are added, preserving the offline-first constraint.
- Tile and row icons use the library's bundled icon set rather than image assets.
- Changes are confined to the desktop client's presentation layer; the shared client behavior layer, APIs, and contracts are unchanged.
- Target remains Windows 10 1809+ / Windows 11 desktop in a single-tenant, offline-first local deployment.

## Dependencies

- The desktop client's existing component-library packages (already referenced).
- The shared client behavior layer for existing bindings, commands, and automation identifiers.

## Out of Scope

- Redesigning Library, Ask, History, My Documents, Admin, or Login beyond incidental consistency of shared text styles.
- Changes to the shared client behavior layer, the workstation API, or any contract.
- New product features or workflows.
- Adding imagery, illustrations, or fonts that require network access or user-installed fonts.
