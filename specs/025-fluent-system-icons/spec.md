# Feature Specification: Fluent System Icons Across the Client

**Feature Branch**: `025-fluent-system-icons`

**Created**: 2026-09-30

**Status**: Draft

**Input**: User description: "use fluent system icons where appropriate"

**Constitution**: v1.3.0 — no amendment needed (presentation-only change in the desktop client; the shared client behavior layer, the workstation API, and all contracts are untouched).

## Clarifications

### Session 2026-09-30

- Q: Should the icon pass cover content-driven interactive controls — the Ask page's "did you mean?" person suggestion buttons and the chat "Sources" expander header — or stay limited to controls with a standard action symbol? → A: Cover the "Sources" disclosure only; leave the repeated person suggestion chips text-only.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Every action can be recognised by its icon (Priority: P1)

A user scanning any screen can tell what a control does from its Fluent icon before reading the label. Today many actions are text-only — refresh, load more, sign in, sign out, create a person, reset a password, retry, upload, cancel, close, ask again, back, view, copy, browse — while a handful of others already carry an icon. After this feature the icon vocabulary is applied consistently, so the same action looks the same everywhere and a returning user recognises it at a glance.

**Why this priority**: Action recognition is the highest-value use of icons: it shortens every task that begins with "find the button", and it is the most visible gap left after the design-system work that standardised type, colour and empty states.

**Independent Test**: Walk every page and dialog and confirm each action control with an unambiguous meaning shows its agreed Fluent icon (beside its label where a label remains, or as the sole content where the control is already icon-only), and that the same action uses the same icon on every surface.

**Acceptance Scenarios**:

1. **Given** any page or dialog, **When** it renders, **Then** every action control whose meaning has an agreed symbol displays that icon.
2. **Given** the same action on two different surfaces (for example "refresh" on Library, History and People, or "copy" on a message, prompt, answer and citation), **When** both render, **Then** they use the same icon.
3. **Given** an action control that keeps its textual label, **When** it renders, **Then** the label remains present and readable next to the icon (no labelled action is reduced to icon-only by this feature).
4. **Given** a surface where an icon would be misleading or add no meaning (a numeric page button, a table column header, a free-text label, the pager ellipsis), **When** it renders, **Then** no icon is forced onto it.
5. **Given** the chat transcript, **When** a message carrying citations renders, **Then** its "Sources" disclosure shows the agreed symbol, while the Ask page's repeated person suggestion chips remain text-only (clarified 2026-09-30).

---

### User Story 2 - State and status read as icons, not only as words or colour (Priority: P1)

A user reading a document list, a people list, a connection row or a message understands state from a Fluent glyph as well as from its colour: document lifecycle (uploading, queued, indexing, ready, failed), a person's active/enabled and locked state, workstation reachability, and success/error/warning messages. State is never carried by colour alone, and a stateful glyph is visually distinct from a resting one.

**Why this priority**: Colour-only or word-only state excludes colour-blind users, is hard to scan at a glance, and is the second half of "where appropriate": icons belong on state, not just on buttons.

**Independent Test**: For each stateful surface, change the state and confirm the glyph changes with it; confirm the same state shows the same glyph on every surface that reports it (Library rows, My Documents rows, the Dashboard recent list, and the People list).

**Acceptance Scenarios**:

1. **Given** a document in a non-final state, **When** its row renders, **Then** the state is conveyed by both a glyph and a colour, and the same glyph is used wherever that state appears.
2. **Given** a person who is enabled or disabled, **When** their row renders, **Then** the two states are distinguished by a glyph as well as by the label beside it — the toggle's glyph matches the action it offers (Activate or Deactivate), so each state reads from its own glyph.
3. **Given** a person who is locked out or not, **When** their row renders, **Then** the locked state is conveyed by a glyph that is clearly distinct from the enabled/disabled glyph.
4. **Given** a success, error or warning message, **When** it renders, **Then** it shows a glyph whose meaning matches the severity.

---

### User Story 3 - Icons stay legible in every theme and are announced to assistive tech (Priority: P2)

A user in light, dark or high-contrast mode, and a screen-reader user, get the same value from the iconography: icons remain visible with sufficient contrast in all three themes, and every icon-only control exposes a meaningful name and tooltip while every icon-plus-label control keeps its label's name. Nothing is clipped at the minimum window size.

**Why this priority**: New icons must not regress the theme and accessibility guarantees the design system establishes; without this, the feature trades one inconsistency for two.

**Independent Test**: Cycle Light / Dark / High Contrast across every affected surface and confirm all icons remain visible; walk the same surfaces with a screen reader and confirm each icon-only control announces its action and never a raw glyph.

**Acceptance Scenarios**:

1. **Given** any affected surface, **When** the theme changes, **Then** every icon remains visible and correctly contrasted with no fixed colour.
2. **Given** an icon-only control (pager arrows, copy, delete, send, an icon-only header action), **When** focus reaches it, **Then** its name and tooltip describe its action.
3. **Given** the minimum supported window size, **When** a labelled control with an icon renders, **Then** neither the icon nor the label is clipped.

---

### User Story 4 - One vocabulary and one size scale (Priority: P2)

A designer or developer introducing a control finds, in one place, the agreed icon for each concept and the agreed size for each kind of surface, so the icon set does not drift back into ad-hoc choices. Every icon used is a real member of the Fluent system icon set, and every size is drawn from the documented scale.

**Why this priority**: Consistency only holds if it is documented and checkable; the vocabulary is what makes the other three stories durable.

**Independent Test**: Compare each concept's icon across nav, tiles, headers, rows and dialogs; confirm one icon per concept, every size drawn from the documented steps, and every referenced icon present in the Fluent system icon set.

**Acceptance Scenarios**:

1. **Given** the set of action and state concepts the client uses, **When** the design-system reference is read, **Then** each concept is listed with exactly one agreed icon.
2. **Given** any icon in the client, **When** its size is measured, **Then** it maps to a documented step (inline, control, row, feature) and no ad-hoc size is introduced.
3. **Given** any referenced icon, **When** it is resolved, **Then** it exists in the Fluent system icon set (no invalid or missing glyph).

---

### Edge Cases

- A concept with no individual-size variant at the documented step (for example double chevrons) uses the documented fallback for that step.
- Concepts that share one idea across surfaces (refresh, copy, back) must resolve to a single icon, not a near-duplicate.
- Per-row controls whose label is dynamic (a person's role, enabled/disabled) keep their accessible names and tooltips; the icon must not replace the meaning the label carried.
- The admin active toggle's glyph follows the action the toggle offers rather than naming the state: because the two states offer opposite actions, the glyph necessarily differs between them, and each state reads from its glyph plus its label (FR-012 keeps the label). A separate read-only state glyph is deliberately not added.
- Controls whose label is already carried by a nearby heading or tile title are not given a redundant icon.
- Repeated content-driven interactive elements — the Ask page's "did you mean?" person suggestion chips — stay text-only: repeating one identical icon per chip adds visual noise without aiding recognition (clarified 2026-09-30). The chat "Sources" disclosure, by contrast, carries an icon because a single disclosure benefits from a meaningful symbol.
- If a chosen glyph is unavailable on Windows 10 1809, a member of the Fluent set that exists on all supported versions is used instead; a raw text character is never substituted.
- Long labels combined with icons must truncate or wrap without displacing controls or clipping at 800×600.
- High Contrast: icons must remain perceivable through theme tokens; no fixed fill or simulated dimming is permitted.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The client's iconography MUST be drawn from the Fluent system icon set; no second icon font or third-party icon package is introduced.
- **FR-002**: Every interactive control whose action has an unambiguous Fluent symbol MUST display that icon.
- **FR-003**: Each action concept MUST use exactly one icon across every surface (at minimum: refresh, load more, sign in, sign out, create person, reset password, retry, upload, cancel, close, ask again, back, view, copy, browse, download, delete, send, the chat "Sources" disclosure, plus the existing navigation, tile and empty-state concepts).
- **FR-004**: Every stateful surface MUST convey its state with a glyph in addition to colour (document lifecycle, person enabled/disabled, person locked, workstation reachability, and success/error/warning messages).
- **FR-005**: All icons MUST be drawn from the documented size scale, with a defined purpose per step; no ad-hoc icon size may be introduced.
- **FR-006**: Icons MUST take their colour from the documented theme roles; no hard-coded colour and no opacity-based dimming may be used.
- **FR-007**: Icon-only controls MUST expose an accessible name and a tooltip; icon-plus-label controls MUST retain the accessible name carried by their label.
- **FR-008**: Icons MUST remain visible, correctly contrasted and unclipped in light, dark and high-contrast themes and at the minimum supported window size (800×600).
- **FR-009**: Icons MUST NOT be added to surfaces where they would be misleading, redundant with an adjacent heading, or where no meaningful symbol exists (for example numeric page buttons, table column headers, free-text labels, the pager ellipsis and the Ask page's repeated person suggestion chips) (clarified 2026-09-30).
- **FR-010**: The documented design-system reference MUST be extended with the new action and state concepts, their agreed icons and any new size guidance, so the vocabulary is the single source of truth.
- **FR-011**: All pre-existing automation identifiers on affected surfaces MUST be preserved, and new interactive elements MUST receive stable identifiers.
- **FR-012**: Labelled controls MUST keep their labels; this feature MUST NOT convert a labelled control into an icon-only control.
- **FR-013**: No behavior change — the shared client behavior layer, the workstation API and all contracts are read-only.

### Key Entities

- **Icon concept**: a product idea (an action such as "refresh" or a state such as "locked") that maps to exactly one Fluent system icon across all surfaces; the concept list is the vocabulary.
- **Icon size step**: a documented size with a defined purpose (inline, control, row, feature) that every icon in the client draws from.
- **State indicator**: a surface that reports a lifecycle or status value (document status, person state, connection status, message severity) and must pair its glyph with its colour role.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of action controls whose meaning has an agreed symbol display that icon (baseline: all text-only action controls across pages and dialogs are covered; the exact baseline is captured at plan time).
- **SC-002**: Every action concept resolves to exactly one icon across every surface it appears on (100% concept consistency, measured by comparing each concept's icon across nav, tiles, headers, rows and dialogs).
- **SC-003**: Every stateful surface distinguishes its states by glyph as well as colour, and no state is signalled by colour alone (100% of stateful surfaces).
- **SC-004**: Zero surfaces use a hard-coded colour or opacity-based emphasis for an icon; 100% resolve their icon colour from the documented theme roles.
- **SC-005**: 100% of icon-only controls expose an accessible name and tooltip; 100% of icon-plus-label controls retain their label's accessible name.
- **SC-006**: All icons remain visible and unclipped in light, dark and high-contrast themes and at 800×600 (100% of theme and size checks).
- **SC-007**: Zero invalid icon references — every referenced icon exists in the Fluent system icon set.
- **SC-008**: All pre-existing automation identifiers on affected surfaces remain present.
- **SC-009**: The solution builds, all existing suites pass with zero assertion changes, and the formatter gate is clean.

## Assumptions

- "Fluent system icons" means the Fluent system icon set already used by the client's component library; this feature extends its use rather than introducing a new icon font or package.
- "Where appropriate" is bounded to actions and states. Icons are added to interactive controls with an unambiguous meaning and to stateful indicators; they are not added to plain text content, table column headers, numeric page buttons, the pager ellipsis, free-text form fields, or controls whose meaning is already carried by an adjacent heading.
- Labels are retained wherever they exist today; the only icon-only controls after this feature are those that are already icon-only.
- The design system established by the prior refinement work is the authority for icon vocabulary, size scale and colour roles; this feature extends that reference and records the new entries there.
- The client targets Windows 10 1809+ and Windows 11 desktop; the chosen icons must exist across that range, with a documented fallback where a specific size variant is unavailable.
- Presentation only: offline-first, single-tenant and citation-grounded behaviour are unaffected.

## Dependencies

- The client's documented design-system reference (`specs/023-design-system-refinement/design-system.md`) for the existing icon vocabulary, size scale, colour roles and shared styles.
- The desktop client's existing component library (already referenced; supplies the Fluent system icon set and the shared icon style).
- The shared client behavior layer for existing bindings, commands and automation identifiers.

## Out of Scope

- New product features, workflows, or copy beyond short supporting text that may accompany a new icon.
- Changing or replacing the icon set, or adding any third-party icon or font package.
- Changes to the shared client behavior layer, the workstation API, or any contract.
- Re-theming the component library or shipping a custom theme.
- Converting labelled controls into icon-only controls.
