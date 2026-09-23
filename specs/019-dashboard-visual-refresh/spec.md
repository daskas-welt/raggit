# Feature Specification: Dashboard Visual Refresh

**Feature Branch**: `019-dashboard-visual-refresh`

**Created**: 2026-09-22

**Status**: Draft

**Input**: User description: "update the look and feel of my UI to look like [Image 1]"

**Constitution**: v1.3.0 — no amendment needed (presentation-only WinUI refresh; Core/API/contracts, single-tenant behavior, offline invariant, and citation grounding remain unchanged).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See a clear work dashboard at a glance (Priority: P1) ★ MVP

An employee opens RAGGit and immediately sees a calm, organized workspace: a distinct blue navigation rail, a welcoming context header, a compact row of useful library/query activity metrics, and primary content panels with clear hierarchy. The visual language should be inspired by the supplied reference image without copying unrelated banking labels or data.

**Why this priority**: The request is primarily a visual reset. A coherent first screen establishes the new look and makes the most common work available without searching through the interface.

**Independent Test**: Launch the app with representative documents and recent queries, then verify that the primary dashboard communicates navigation, current context, and library/query activity within one viewport at a representative desktop size.

**Acceptance Scenarios**:

1. **Given** an authenticated employee at a representative desktop width, **When** the app opens, **Then** the navigation rail, context header, metric tiles, and primary content panels are visually distinct and readable without excessive unused margins.
2. **Given** a library with documents and recent activity, **When** the dashboard renders, **Then** metrics and summaries reflect existing data and link to the relevant existing page or action.
3. **Given** an empty or newly installed library, **When** the dashboard renders, **Then** the same hierarchy remains intact and empty panels provide a clear next action instead of looking broken.

---

### User Story 2 - Move through the app with a consistent visual system (Priority: P1)

An employee can identify the current page and move between Ask, Library, History, My Docs, and other available destinations using a persistent left navigation rail. Selected, hovered, and disabled states are obvious, and page headers and content panels share the same spacing, typography, and surface language.

**Why this priority**: The reference image’s strongest organizing feature is its persistent navigation and consistent content grid. Without that system, an isolated dashboard restyle would feel disconnected from the rest of the app.

**Independent Test**: Navigate through every existing destination using mouse and keyboard, confirming the current destination is obvious and the shell remains stable while only page content changes.

**Acceptance Scenarios**:

1. **Given** any supported page, **When** the employee selects a navigation destination, **Then** the selected destination is visually identified and the destination opens using the shared page structure.
2. **Given** keyboard-only use, **When** the employee tabs through the shell, **Then** every navigation item is reachable in logical order with visible focus and Enter activates it.
3. **Given** a narrow window at or below 720px, **When** the shell renders, **Then** navigation and content reflow without clipping or trapping controls.

---

### User Story 3 - Read dense information without losing trust (Priority: P2)

An employee can scan document counts, recent queries, ingestion state, and other existing status information in compact panels that use restrained color, whitespace, and labels. Alerts, citations, empty states, and failures remain prominent and understandable rather than being hidden by decorative styling.

**Why this priority**: The new visual direction must improve scanning without weakening the product’s grounded-answer and operational feedback guarantees.

**Independent Test**: Exercise populated, empty, loading, error, and citation-bearing states, then verify that each state remains legible and that no visual treatment implies facts that are not present in the underlying data.

**Acceptance Scenarios**:

1. **Given** a query with grounded citations, **When** the result is displayed, **Then** citations and the answer remain more prominent than decorative metrics and are reachable by keyboard and screen reader.
2. **Given** a loading or failed request, **When** the status changes, **Then** the existing user-facing progress or error message remains visible in a dedicated status surface with no misleading success styling.
3. **Given** Light, Dark, or High Contrast mode, **When** the employee views any refreshed surface, **Then** text, icons, focus, status, and selection states remain distinguishable.

---

### Edge Cases

- No documents or recent queries: metrics show an intentional zero/empty state and offer the existing upload or Ask action where applicable.
- Very long document names, query text, or usernames: content truncates or wraps without changing panel width or hiding actions.
- 100+ documents or history entries: collection controls remain smooth and virtualized; the refresh does not introduce nested-scroll traps.
- Session expiry or workstation connection loss: the existing session/error path remains visible and usable within the refreshed shell.
- High Contrast mode: decorative fills do not replace semantic borders, text, icons, or focus indicators.
- Window resize between desktop and narrow widths: no primary action becomes unreachable.
- Admin-only controls: the refreshed shell does not expose admin destinations or actions to employees who do not already have permission.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The application MUST provide a shared shell with a persistent left navigation rail, a distinct selected destination state, a user/session area, and a page content region.
- **FR-002**: The refreshed shell MUST provide a dashboard landing surface containing a context header, compact metric summaries based only on existing application data, and clearly grouped primary content panels.
- **FR-003**: The visual system MUST use a restrained blue navigation accent, neutral light workspace surfaces, clear typography hierarchy, compact metric tiles, and chart or trend treatments only where existing data supports them; it MUST NOT invent financial terminology or unsupported data.
- **FR-004**: Existing destinations, commands, navigation permissions, query behavior, document behavior, upload behavior, citations, and status outcomes MUST remain functionally unchanged.
- **FR-005**: Every refreshed interactive element MUST remain keyboard-operable with visible focus, provide an accessible name, and meet the project’s established touch-target and screen-reader requirements.
- **FR-006**: The shell and refreshed panels MUST render legibly in Light, Dark, and High Contrast modes using semantic theme resources rather than hard-coded color literals.
- **FR-007**: The refreshed layout MUST adapt from a representative desktop width to 720px or narrower without clipping primary content, hiding required actions, or introducing nested collection scroll regions.
- **FR-008**: Existing list and grid surfaces MUST retain virtualization and must not be wrapped in a way that disables smooth scrolling for large collections.
- **FR-009**: Loading, success, error, empty, session-expired, and citation-bearing states MUST remain visually and semantically distinguishable in every refreshed surface.
- **FR-010**: The design decisions for each changed surface MUST be recorded against the supplied reference image and the existing Windows-native design baseline before implementation begins.
- **FR-011**: The feature MUST remain presentation-only: no new server endpoints, data fields, tenant concepts, query-time WAN calls, or changes to retrieval/generation policy may be introduced.

### Key Entities

- **Dashboard summary**: A presentation of existing document, query, ingestion, or history information; it has no independent source of truth.
- **Navigation destination**: An existing application route/page with a label, icon, selected state, and permission visibility.
- **Status state**: Existing loading, success, error, empty, session, or citation-bearing feedback presented in the refreshed visual language.
- **Theme surface**: A shell, panel, metric tile, chart area, or status surface whose colors, typography, spacing, and contrast adapt to the active system theme.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In a 10-person walkthrough using representative data, at least 9 participants identify the current page and locate the primary Ask and Library actions within 10 seconds of launch.
- **SC-002**: At least 90% of participants complete navigation to each existing non-admin destination using the refreshed shell on their first attempt, with no instruction about the new layout.
- **SC-003**: A representative 100-document dataset remains smooth during scrolling and resizing, with zero clipped primary actions or unreachable collection controls in desktop and narrow-window checks.
- **SC-004**: Light, Dark, and High Contrast walkthroughs find zero unreadable text, missing focus indicators, or ambiguous selected-navigation states across all refreshed surfaces.
- **SC-005**: Across 20 loading, success, error, empty, session-expired, and citation-bearing trials, every outcome is visible, correctly labeled, and not obscured by the visual refresh.
- **SC-006**: Existing unit, contract, integration, offline, and UI tests pass without assertion changes caused by presentation-only work; no query-time WAN behavior is introduced.

## Assumptions

- The supplied image is a visual direction, not a request to reproduce its banking domain content, exact copy, proprietary assets, or pixel dimensions.
- The blue navigation rail is the signature reference treatment; the rest of the palette remains theme-aware and Windows-native rather than forcing the reference image’s colors into Dark or High Contrast mode.
- Dashboard summaries use only data already available to the client. If a desired metric is not already available, the UI will show an existing count or omit the metric rather than add a new API contract.
- Existing RAGGit pages remain available; this feature unifies their visual shell and landing hierarchy instead of replacing their workflows.
- The current WinUI design baseline from `011-ui-redesign`, including Mica, semantic theme resources, native controls, accessibility, and virtualization guidance, remains authoritative.
- Desktop is the primary composition; narrow-window behavior is reflow and stacking, not a separate mobile product.

## Dependencies

- `011-ui-redesign`: existing WinUI shell, theme, accessibility, virtualization, and native-control direction.
- `012-fonticon-glyphs`: icon and glyph conventions for navigation and action affordances.
- `013-remove-upload-nav`: current navigation entry-point decisions that must remain intact.
- `015-file-upload-ui` and `017-multi-file-upload`: existing upload status and workflow surfaces that must retain behavior while adopting shared styling.
- `016-admin-redesign`: admin surface conventions and admin-only visibility rules.
