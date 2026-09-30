# Feature Specification: Application Icon

**Feature Branch**: `024-client-app-icon`

**Created**: 2026-09-30

**Status**: Draft

**Input**: User description: "use an app icon"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The application is recognisable in the Windows shell (Priority: P1)

A Windows user launches Raggit, pins it, or looks at it in Alt-Tab and sees the product's own icon
rather than the generic placeholder an application shows when it has no identity. The same icon appears
on the application file itself, so an employee told to "run Raggit" can pick it out of a folder or from
a shortcut.

**Why this priority**: Outside its own window the product currently presents no identity at all, and the
shell surface is the first thing a user sees; it is also the cheapest credibility and findability
signal available.

**Independent Test**: Launch the application and inspect each shell surface — the window's title bar,
the taskbar button, the Alt-Tab entry, the application file in the file system, and a shortcut created
from it — confirming each shows the Raggit icon.

**Acceptance Scenarios**:

1. **Given** the application on a Windows machine, **When** the user launches it, **Then** the taskbar button and the window's title bar show the Raggit icon rather than a generic placeholder.
2. **Given** the running application, **When** the user switches windows with Alt-Tab, **Then** the entry for Raggit shows the same icon.
3. **Given** the application file in a folder, **When** the user browses that folder, **Then** the file shows the same icon.

---

### User Story 2 - The icon reads at every size and background the shell asks for (Priority: P2)

A user sees a crisp, unmistakable mark whether the shell renders it tiny (taskbar, title bar), medium
(Alt-Tab, file lists), or large (tiles, previews), and whether their shell is light or dark.

**Why this priority**: A mark that turns to mush at taskbar size, or disappears on a dark taskbar, is
worse than no icon; this is what makes the identity usable rather than decorative.

**Independent Test**: Display the icon at the shell's smallest and largest sizes against both a light and
a dark background and review it, confirming the mark is still identifiable and free of visible artefacts.

**Acceptance Scenarios**:

1. **Given** the icon, **When** it is displayed at taskbar size, **Then** the mark is still recognisable and free of visible blur, fringing, or detail that vanishes.
2. **Given** the icon, **When** it is displayed at the largest size the shell uses, **Then** it renders cleanly without upscaling artefacts.
3. **Given** a light and a dark shell background, **When** the icon is displayed on each, **Then** the mark stays distinguishable and does not rely on colour alone.

---

### User Story 3 - The identity travels with the product (Priority: P3)

An employee who receives the published application build sees the same identity there as a developer
does: the icon is part of what ships, not something applied machine by machine.

**Why this priority**: The published build is how the product reaches people; an identity that only
exists in a development environment is not delivered.

**Independent Test**: Take the published desktop build described in `docs/publish.md`, run it on a
machine that has never seen the product, and confirm every surface from User Story 1 shows the icon.

**Acceptance Scenarios**:

1. **Given** the published application build on a clean machine, **When** the application is launched, **Then** the shell surfaces show the Raggit icon with no manual setup.
2. **Given** the published application file, **When** it is viewed in the file system, **Then** it shows the Raggit icon.

---

### Edge Cases

- **Smallest shell sizes**: the mark must survive the shell's smallest rendering without losing its shape; a simplified small-size treatment is acceptable as long as the identity stays the same.
- **Dark and high-contrast shells**: identity must not be conveyed by colour alone, and the mark must stay visible when the shell background is dark or high-contrast mode is on.
- **Windows icon cache**: after an update the shell can temporarily show the icon it cached for the previous build; the shipped identity must still be correct.
- **Several copies of the build running at once**: the icon must identify the product in taskbar grouping rather than the executable's file name doing that work.
- **Text inside the mark**: any wording in the mark must use the product name as the shell presents it (`Raggit`), and the mark must still read when no text is legible at all.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The application MUST present its own icon, not a generic placeholder, on every surface where Windows shows it: the window's title bar, the taskbar button, the Alt-Tab switcher, and the application file as shown by the file system.
- **FR-002**: The same mark MUST appear on every surface; size-adapted variants are allowed, a different identity per surface is not.
- **FR-003**: The icon MUST remain recognisable at the shell's smallest sizes and render cleanly at the largest size the shell uses, with no visible blur, fringing, or upscaling artefacts.
- **FR-004**: The mark MUST remain distinguishable on both light and dark shell backgrounds and MUST NOT convey the product's identity by colour alone.
- **FR-005**: The identity MUST ship with the published application build and require no per-machine setup.
- **FR-006**: The mark MUST be consistent with the product's existing brand treatment — the product name `Raggit` and the client's established colour and glyph vocabulary — and MUST NOT reproduce third-party, supplied-reference, or unrelated artwork.
- **FR-007**: The feature MUST be presentation-only: no change to application behaviour, workflows, automation identifiers, window titles, the shared client behaviour layer, the workstation service, or any interface contract.
- **FR-008**: The icon MUST use one fixed brand colour chosen for the product, identical on every machine and for every user, and MUST NOT follow the user's Windows accent setting. The value MUST be defined once and recorded with the identity, so that every size variant — and any future in-app use — draws on the same value.

### Key Entities

Not applicable — the feature introduces presentation identity only, no data or persistence.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the surfaces in FR-001 show the Raggit icon instead of a placeholder, confirmed on a clean Windows session.
- **SC-002**: The mark is identifiable and free of visible artefacts at both the smallest and the largest shell size, reviewed against light and dark backgrounds.
- **SC-003**: The published build from `docs/publish.md` shows the identity on a machine that has never run the application, with zero manual steps.
- **SC-004**: Zero third-party or supplied-reference assets are introduced, and no colour appears outside the documented brand treatment.
- **SC-005**: The solution builds and the existing test and format gates stay green with zero assertion changes.

## Assumptions

- The icon is text-free at the sizes the shell renders most often; where the mark carries wording, it uses the product name as the shell presents it today (`Raggit`).
- The artwork is created within the repository. No network access, third-party or supplied-reference assets, and no user-installed fonts are introduced.
- Scope is the desktop client's identity as Windows presents it. The in-app brand treatment (the navigation wordmark, the dashboard hero, the sign-in surface) keeps its current design, as does the workstation service.
- The distribution shape is unchanged: the client keeps shipping as the published build described in `docs/publish.md`; installer-level identity (uninstall entries, store listing) is out of scope.
- The fixed brand colour is chosen during planning from the product's existing visual identity and recorded in one place alongside the identity. It is the product's first fixed colour: every other colour the client shows keeps coming from the component library's theme tokens, and the in-app accent keeps following each user's Windows accent — so on a given machine the icon and the app's own accent may differ. That divergence is accepted by this decision, and the mark's shape carries the identity while the colour reinforces it.
- Windows may cache a file icon for a previously seen build; a stale cache entry is an environment quirk rather than a defect in the shipped identity.
- The design-system reference from `023-design-system-refinement` governs any colour or glyph choice made here, and `020-raggit-branding-refresh` governs the product name.

## Dependencies

- The client's existing brand treatment and design-system reference (`023-design-system-refinement`) for any colour or glyph decision, and `020-raggit-branding-refresh` for the product name.
- `docs/publish.md` for the artifact the identity must ship with.
- The Windows shell's own icon size and background requirements; no new dependency, package, or service.

## Out of Scope

- Re-designing the in-app brand treatment (navigation wordmark, dashboard hero, sign-in surface) or changing the product name.
- Installer, store-listing, or uninstall-entry identity.
- Marketing collateral, website, or documentation imagery beyond the application icon itself.
- Any behavioural change, new interface, or new dependency.
