# Feature Specification: Raggit Branding Refresh

**Feature Branch**: `020-raggit-branding-refresh`

**Created**: 2026-09-22

**Status**: Draft

**Input**: User description: "Update colors according to [Image 1] - on left area at the top show the name of the app called Raggit"

**Constitution**: v1.3.0 — no amendment needed (presentation-only WinUI branding and color refresh; server behavior, client data contracts, single-tenant operation, offline invariant, and citation grounding remain unchanged).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Recognize Raggit immediately (Priority: P1) ★ MVP

An employee opens the application and immediately recognizes it as Raggit. The top of the left navigation area shows the product name `Raggit` with clear hierarchy, while the navigation rail and workspace use the blue-and-light palette shown in the supplied reference image.

**Why this priority**: Product identity and first-glance visual clarity are the direct request. The app name and signature color treatment must be visible before secondary polish matters.

**Independent Test**: Launch an authenticated session at a representative desktop width and verify that `Raggit` appears at the top of the left navigation area, the rail uses the intended blue treatment, and the main workspace remains readable and usable.

**Acceptance Scenarios**:

1. **Given** an authenticated employee on any top-level page, **When** the shell renders, **Then** `Raggit` is visible at the top of the left navigation area above the navigation destinations.
2. **Given** the shell is rendered in the reference-inspired Light theme, **When** the employee scans the window, **Then** the navigation rail is a distinct blue region and the workspace is a calm white/light neutral region with readable contrast.
3. **Given** the employee selects any existing navigation destination, **When** the selected state changes, **Then** the branding and rail remain stable while the selected destination is still clearly distinguishable.

---

### User Story 2 - Use the branded shell in every supported theme and size (Priority: P1)

An employee can use the branded shell in Light, Dark, and High Contrast modes, and at narrow window widths. The product name, navigation labels, selected state, focus indicator, and workspace content remain visible without clipping or relying on color alone.

**Why this priority**: A brand treatment that only works in one screenshot would regress accessibility and the existing responsive shell requirements.

**Independent Test**: Walk through the shell with keyboard and Narrator in Light, Dark, and High Contrast modes at desktop and 720px-or-narrower widths.

**Acceptance Scenarios**:

1. **Given** Light, Dark, or High Contrast mode, **When** the shell renders, **Then** `Raggit`, navigation labels, selected state, focus state, and workspace text remain distinguishable.
2. **Given** a window at or below 720px, **When** the navigation changes to its compact/top layout, **Then** `Raggit` remains available through the shell’s responsive branding treatment and no primary destination is clipped.
3. **Given** keyboard-only navigation, **When** the employee tabs through the shell, **Then** the branded navigation remains reachable in logical order with visible focus and existing Automation IDs unchanged.

---

### User Story 3 - Keep existing work intact while the appearance changes (Priority: P2)

An employee continues using Dashboard, Library, Ask, History, My Docs, Admin where permitted, uploads, grounded answers, citations, profile information, and status messages without learning new workflows. Only the visual identity and color treatment change.

**Why this priority**: Branding must not compromise the RAGGit product’s core workflows or trust signals.

**Independent Test**: Navigate through every existing destination and exercise representative loading, empty, error, citation, upload, and admin-gated states after the branding refresh.

**Acceptance Scenarios**:

1. **Given** any existing page or dialog, **When** the branded resources are applied, **Then** its commands, permissions, content, status messages, citations, and upload entry point behave as before.
2. **Given** a loading, error, empty, or success state, **When** it is displayed, **Then** its semantic meaning remains understandable without color being the only signal.
3. **Given** an employee who is not an admin, **When** the branded shell renders, **Then** Admin remains hidden and no additional privileged action is exposed.

### Edge Cases

- Long or localized navigation labels: `Raggit` and navigation labels remain readable without overlapping icons or window controls.
- High Contrast mode: system-aware colors take precedence over decorative brand blue while preserving the product name and selected/focus states.
- Narrow window: the branding treatment does not consume space needed to reach navigation destinations.
- Configuration error or session expiry: existing error/session surfaces remain visible and understandable within the branded shell.
- Dark theme: the blue accent is adapted for contrast rather than copied as an unreadable Light-theme color.
- Existing custom dashboard profile card: the logged-user information remains visually distinct from product branding.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The shell MUST display the exact product name `Raggit` at the top of the left navigation area above or alongside the navigation destinations.
- **FR-002**: The Light-theme shell MUST use a distinct reference-inspired blue navigation region and a white/light neutral workspace region with readable text and controls.
- **FR-003**: The branded color system MUST define appropriate semantic variants for Light, Dark, and High Contrast modes; feature surfaces MUST not depend on hard-coded color literals.
- **FR-004**: The product name, navigation labels, selected navigation state, keyboard focus, and user/session context MUST remain distinguishable in every supported theme.
- **FR-005**: At 720px or narrower, the responsive navigation MUST preserve access to `Raggit`, all existing permitted destinations, selected state, and focus state without clipping or overlap.
- **FR-006**: Existing navigation Automation IDs, route tags, role gating, page commands, upload behavior, profile card behavior, status outcomes, grounded answers, and citations MUST remain unchanged.
- **FR-007**: The branding refresh MUST remain presentation-only and MUST NOT add server endpoints, data fields, tenant concepts, query-time WAN calls, or retrieval/generation behavior.
- **FR-008**: The branded shell MUST use a semantic product-name surface that can be read by keyboard and screen-reader users; product identity MUST NOT be conveyed by color alone.
- **FR-009**: The visual spacing and logo/name treatment MUST follow the supplied reference’s left-rail hierarchy without copying unrelated product labels, proprietary assets, or unsupported content.

### Key Entities

- **Product branding**: The `Raggit` name and its semantic visual treatment in the application shell.
- **Navigation rail**: The existing shell region containing top-level destinations, selected state, focus state, and responsive layout.
- **Theme surface**: The semantic colors used by the rail, workspace, product name, navigation labels, selected state, and focus state across supported themes.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In a 10-person walkthrough, at least 9 participants identify the product as `Raggit` within 5 seconds of launch.
- **SC-002**: 100% of tested shell destinations retain their existing navigation behavior, permission visibility, and Automation IDs across Light, Dark, High Contrast, and narrow-window checks.
- **SC-003**: A visual review of all branded shell states finds zero clipped product-name labels, navigation labels, selected states, or focus indicators at representative desktop and 720px-or-narrower widths.
- **SC-004**: At least 90% of keyboard/Narrator walkthrough participants can identify the product name, current destination, and primary navigation actions without color-dependent instructions.
- **SC-005**: Existing unit, contract, integration, offline, and UI/build checks pass without behavior or assertion changes attributable to the branding refresh.

## Assumptions

- The supplied image is a visual direction for palette and shell hierarchy, not a request to copy its exact logo, labels, content, or dimensions.
- `Raggit` is rendered as text in the initial branding refresh; no new image asset or logo file is required.
- The existing blue rail resources and dashboard surface resources are extended or adjusted rather than replaced with a second unrelated theme system.
- Existing profile information, dashboard metrics, and workflows remain unchanged.
- High Contrast uses system brushes even when those differ from the reference image’s blue palette.
- Desktop remains the primary composition; narrow-window behavior reflows the existing shell rather than creating a separate mobile layout.

## Dependencies

- `011-ui-redesign`: existing native WinUI shell, theme, accessibility, and responsive baseline.
- `019-dashboard-visual-refresh`: current dashboard rail, profile card, metric surfaces, and responsive navigation that this branding refresh must refine rather than regress.
- `012-fonticon-glyphs`: existing icon and accessible-name conventions.
- `016-admin-redesign`: established role-gating and theme-aware status conventions.
