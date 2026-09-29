# Feature Specification: WPF-UI Modernization

**Feature Branch**: `021-wpfui-modernization`

**Created**: 2026-09-29

**Status**: Draft

**Input**: User description: "Update UI/UX to use WPF UI at https://github.com/lepoco/wpfui (docs https://wpfui.lepo.co/); use Segoe Fluent Icons."

**Constitution**: v1.3.0 — no amendment needed (presentation-only; Core, APIs, contracts untouched).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - One Fluent design language across every screen (Priority: P1) ★ MVP

A user moves through dashboard, library, ask, history, my-documents, admin, settings, and login and every control reads as the same product: same button, input, list, card, and icon language, with no stragglers that still look like default Windows controls.

**Why this priority**: Inconsistency is the user-visible cost of a half-adopted component library; this is the core of the request.

**Independent Test**: Open every primary screen and compare controls against a reference Fluent surface; verify no default-styled control remains and a written mapping exists for each control family.

**Acceptance Scenarios**:

1. **Given** any primary screen, **When** it renders, **Then** every interactive control uses the shared Fluent control set.
2. **Given** the audit document, **When** a reviewer checks any control family, **Then** it records the chosen equivalent, or the reason none exists.

---

### User Story 2 - Correct in every theme, zero fixed colors (Priority: P1)

A user switches between light, dark, and high-contrast (or follows the system) and every surface, border, text, status color, and icon adapts.

**Why this priority**: The remaining hard-coded colors and code-behind brushes are the last barrier to a coherent themed UI.

**Independent Test**: Cycle themes with every screen and dialog open; confirm no stale or fixed color survives, including status chips and chat bubbles.

**Acceptance Scenarios**:

1. **Given** any screen, **When** the theme changes, **Then** all colors update from theme resources.
2. **Given** high contrast, **When** any screen renders, **Then** text, borders, and focus visuals stay legible.
3. **Given** a fresh install, **When** the app starts, **Then** it follows the system theme.

---

### User Story 3 - Fluent iconography replaces text glyphs (Priority: P2)

A user sees crisp, theme-recolored Fluent icons everywhere an action is represented, including the library/history pager and status surfaces, instead of text characters like `« ‹ › »`.

**Why this priority**: Literal glyphs are the one visible spot that breaks the icon language and can vanish in High Contrast.

**Independent Test**: Walk every screen; assert no button content is a literal glyph and each icon-only control announces its purpose.

**Acceptance Scenarios**:

1. **Given** the pager, **When** rendered, **Then** first/previous/next/last show Fluent chevron icons, themed and enabled-state aware.
2. **Given** an icon-only control, **When** focused by a screen reader, **Then** its accessible name and tooltip are announced.
3. **Given** the minimum supported OS, **When** icons render, **Then** they appear without any font installed by the user.

---

### User Story 4 - Unified shell behaviors (Priority: P2)

Dialogs, notifications, and navigation behave identically wherever they are triggered.

**Why this priority**: A single shared surface per concern keeps behavior predictable and reduces duplicate infrastructure.

**Independent Test**: Trigger modal, notification, and navigation from several screens and confirm one shared surface each, with focus returning after dismissal.

**Acceptance Scenarios**:

1. **Given** any modal, **When** it closes, **Then** focus returns to the invoking control.
2. **Given** a background action completes, **When** a notification appears, **Then** it uses the shared notification surface.

---

### User Story 5 - Settings and admin adopt rich Fluent patterns (Priority: P3)

Configuration and user management screens use card/settings-row patterns with clear labels and grouping rather than stacked default controls.

**Why this priority**: These dense forms benefit most from structured patterns, but they are not on the primary query path.

**Independent Test**: Operate settings and admin entirely by keyboard and screen reader.

**Acceptance Scenarios**:

1. **Given** the settings screen, **When** a user changes a preference, **Then** the row conveys label, value, and description.
2. **Given** the admin list, **When** a user acts on a row, **Then** the action is reachable and labeled.

---

### Edge Cases

- Theme switched while a dialog or notification is open.
- Minimum window size: all primary content reachable, nothing clipped.
- Windows 10 1809: icon and typography fallbacks keep everything visible.
- Existing empty, error, and "original unavailable" states remain themed.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Every interactive control MUST use the component library's Fluent equivalent where one exists; where none exists, the audit MUST record the reason and the fallback.
- **FR-002**: All color and brush references MUST originate from library theme resources; zero hard-coded color literals may remain in the desktop client (XAML or code-behind).
- **FR-003**: The app MUST render correctly in light, dark, and high-contrast, and MUST follow the system theme by default.
- **FR-004**: Icons MUST be rendered through the library's Fluent icon set, replacing literal text glyphs, and MUST recolor from theme foreground.
- **FR-005**: Icon rendering MUST work on the minimum supported OS without any user-installed font.
- **FR-006**: Navigation, modal dialogs, and transient notifications MUST each use a single shared shell surface.
- **FR-007**: Typography MUST come from the library's typography styles rather than ad-hoc font sizes.
- **FR-008**: Existing automation identifiers MUST be preserved; every new interactive element MUST receive a stable identifier.
- **FR-009**: All primary actions MUST be keyboard reachable with visible focus; icon-only controls MUST carry accessible names and tooltips.
- **FR-010**: A control-mapping document MUST be produced covering every control family in the client, listing adopted equivalent or justified exception.
- **FR-011**: No behavior change — shared client behavior layer, APIs, and contracts are read-only.
- **FR-012**: Lists MUST remain virtualized (no scroll-owning element wrapping a virtualizing collection).
- **FR-013**: At minimum window size, all primary content MUST remain reachable and unclipped.

### Key Entities

- **Control mapping catalog**: the per-family record of adopted equivalent / exception and rationale.
- **Theme resource layer**: application-level light/dark/high-contrast resources for color and typography.
- **Icon layer**: the Fluent icon set and the glyphs it replaces.
- **Shared components**: chat, pagination footer, status chip.
- **Page surfaces**: dashboard, library, ask, history, my-documents, admin, login, settings.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Zero hard-coded color literals remain in the desktop client (baseline: 5 in XAML + 4 code-behind brushes).
- **SC-002**: 100% of raw interactive controls are either converted to a Fluent equivalent or recorded with a justified exception in the mapping catalog (baseline: 9 raw buttons, 12 list views, 9 list boxes, 8 progress bars, 2 combo boxes, 2 radio buttons, 1 check box, 2 expanders).
- **SC-003**: Zero literal text-glyph controls remain (baseline: 4 pager buttons).
- **SC-004**: Every primary screen passes a light/dark/high-contrast visual check with no unreadable text.
- **SC-005**: 100% of primary-scenario interactive elements retain stable automation identifiers and pass a keyboard-only walkthrough.
- **SC-006**: Icons render on Windows 10 1809+ with no font installed by the user.
- **SC-007**: Build and all test suites pass with zero assertion changes; formatter gate clean.

## Assumptions

- Scope is a full audit-and-modernize pass over the desktop client, not a greenfield rewrite.
- The component library already referenced by the project remains the target; no competing UI library is introduced.
- Icon strategy defaults to the library's bundled Fluent icon set; explicit OS-font glyphs are used only where the bundled set has no match, and only if they satisfy the minimum-OS requirement.
- Changes are confined to the desktop client's presentation layer (XAML, code-behind, resources); shared ViewModels and APIs are unchanged.
- Target is Windows 10 1809+/11 desktop in a single-tenant, offline-first local deployment.

## Dependencies

- The desktop client's existing UI library packages.
- The shared client behavior layer for existing bindings and automation identifiers.

## Out of Scope

- Changes to shared behavior layers, APIs, or contracts.
- New product features or workflows.
- Cross-platform desktop support.
- Retiring or reviving legacy WinUI/MAUI client work.
