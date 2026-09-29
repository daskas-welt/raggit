# Feature Specification: Design-System Refinement

**Feature Branch**: `023-design-system-refinement`

**Created**: 2026-09-29

**Status**: Draft

**Input**: User description: "Improve icons, typography and colors" — applied as a shared, documented design system across the desktop client.

**Constitution**: v1.3.0 — no amendment needed (presentation-only; shared client behavior layer, APIs, and contracts untouched).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Every screen announces itself the same way (Priority: P1)

A user moving between Library, Ask, History, My Documents, Admin, and Settings sees the same page-opening pattern everywhere: a page title with a short supporting line that explains the surface, and supporting text in the same visual weight on every page — never lighter on one screen than another because a different technique was used.

**Why this priority**: Typography is the cheapest, highest-leverage consistency signal; mixed emphasis techniques are the most visible residue of ad-hoc styling.

**Independent Test**: Walk every primary page; confirm each opens with a title plus a supporting line, and that supporting text uses one consistent treatment (no screen whose secondary text is dimmer than another's).

**Acceptance Scenarios**:

1. **Given** any primary page, **When** it renders, **Then** it shows a page title and a one-line supporting description beneath it.
2. **Given** any two pages, **When** their supporting text is compared, **Then** both use the same emphasis treatment and the same type step.
3. **Given** the type hierarchy, **When** a heading, body line, and meta line are compared, **Then** each maps to a distinct documented type step (no ad-hoc sizes).

---

### User Story 2 - Colour behaves in every theme (Priority: P1)

A user in light, dark, or high-contrast mode sees every emphasis level read correctly: supporting text stays legible, accent surfaces keep readable text on them, status colours communicate the same meaning, and no text becomes washed out because dimming was simulated rather than expressed with a theme token.

**Why this priority**: Simulated dimming (reduced opacity) can fall below legibility in high contrast; token-based emphasis is what makes the three themes trustworthy.

**Independent Test**: Cycle Light / Dark / High Contrast across every affected screen; confirm all text and emphasis levels stay legible and semantically consistent.

**Acceptance Scenarios**:

1. **Given** any affected screen, **When** the theme changes, **Then** every text and emphasis level remains legible with no fixed or simulated colour.
2. **Given** text placed on an accent-coloured surface, **When** it renders, **Then** it uses the on-accent convention for text (not the accent-coloured text convention).
3. **Given** a status indicator, **When** its state changes, **Then** its colour and glyph both communicate the state using the shared status tokens.

---

### User Story 3 - Iconography is one vocabulary (Priority: P2)

A user sees the same symbol for the same idea everywhere, icon sizes step consistently rather than drifting, and stateful imagery (status, active/selected) is visually distinct from resting imagery.

**Why this priority**: Icon drift is subtle but cumulative; a documented size scale and one-glyph-per-concept rule stops it recurring.

**Independent Test**: Compare each concept's glyph across nav, tiles, headers, and rows; confirm one symbol per concept, sizes drawn from the documented scale, and stateful glyphs visually distinct.

**Acceptance Scenarios**:

1. **Given** a concept (Library, Ask, History, My Documents, Admin, Settings, Refresh, Upload, Download, Delete, Copy, Send), **When** it appears anywhere, **Then** it uses one agreed symbol.
2. **Given** any icon, **When** its size is measured, **Then** it is drawn from the documented scale (inline / control / row / tile).
3. **Given** a status or active state, **When** it renders, **Then** it is visually distinguished from the resting state.
4. **Given** the active navigation destination, **When** the pane renders, **Then** it is unambiguously distinguishable from the others.

---

### User Story 4 - Data surfaces are easy to scan (Priority: P2)

A user reading a table can follow a row across its columns without losing their place, and a list with no content shows a purposeful empty state with imagery rather than a bare sentence.

**Why this priority**: The document and people tables are the densest surfaces; row tracking and empty states materially affect usability, but they are not on the primary query path.

**Independent Test**: On a populated Library/People table, confirm rows are visually separated; on each empty list, confirm an empty state with imagery and a clear next step.

**Acceptance Scenarios**:

1. **Given** a populated table, **When** it renders, **Then** adjacent rows are visually separated so a row can be followed across columns.
2. **Given** a table header row, **When** it renders, **Then** it is distinguishable from data rows.
3. **Given** a list with no items, **When** it renders, **Then** it shows an empty state with a symbol, a title, and a next-step hint.

---

### Edge Cases

- High Contrast: emphasis levels and row separators remain perceivable (separators must not rely on subtle colour alone).
- Minimum window size (800×600): larger empty-state imagery and row separators do not cause clipping.
- Very long page-header supporting lines or values: they truncate or wrap without displacing controls.
- Tiles/headers at narrow widths: icon chips keep their size while text truncates.
- A concept whose glyph has no individual-size variant at the documented step (e.g. double chevrons): a documented fallback applies.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A single documented design-system reference MUST define the type steps, the colour-token roles, the icon size scale, and the state (resting vs stateful) conventions.
- **FR-002**: Supporting/secondary text MUST use one consistent emphasis treatment everywhere it appears.
- **FR-003**: No text emphasis may be produced by reducing overall opacity; emphasis MUST come from theme token roles.
- **FR-004**: Every primary page MUST present a title with a short supporting description line.
- **FR-005**: All colour MUST originate from the library's theme tokens, in the documented roles: text levels, accent fill, accent decoration, text-on-accent, status, and surface.
- **FR-006**: Text placed on an accent surface MUST use the text-on-accent role, never the accent-coloured-text role.
- **FR-007**: Icons MUST be drawn from a documented size scale with a defined purpose per step.
- **FR-008**: Each product concept MUST use exactly one symbol across all surfaces.
- **FR-009**: Status indicators MUST distinguish states by both colour and glyph, using the filled variant where the resting variant would read as ambiguous.
- **FR-010**: The active navigation destination MUST be visually distinguished from inactive destinations.
- **FR-011**: Tabular data surfaces MUST provide visual separation between adjacent rows and distinguish the header row from data rows.
- **FR-012**: List surfaces with no items MUST present an empty state containing a symbol, a title, and a next-step hint.
- **FR-013**: The repeated page-header and empty-state treatments MUST be expressed as shared styles rather than repeated inline attributes.
- **FR-014**: Existing automation identifiers on affected surfaces MUST be preserved; new interactive elements MUST receive stable identifiers.
- **FR-015**: Affected surfaces MUST remain legible and unclipped in light, dark, and high-contrast themes and at the minimum window size (800×600).
- **FR-016**: No behavior change — the shared client behavior layer, the workstation API, and all contracts are read-only.

### Key Entities

Not applicable — this feature introduces no data, persistence, or API surface; it changes presentation and documentation only.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Zero occurrences of opacity-based text emphasis remain on the affected surfaces (baseline: 20 usages, all removed by this feature; the rule must hold for new work).
- **SC-002**: 100% of primary pages present a title **and** a supporting description line.
- **SC-003**: 100% of the affected interactive controls resolve their colours from theme tokens, with zero hard-coded colour values.
- **SC-004**: The page-header and empty-state patterns are each served by one shared style, reused by every affected page (no affected page re-declares them inline).
- **SC-005**: Every icon references a valid library icon name and a size drawn from the documented scale.
- **SC-006**: Every data table shows row separation and a distinct header row; every empty list shows an icon-bearing empty state.
- **SC-007**: The active navigation destination is visually distinguishable in all three themes.
- **SC-008**: All pre-existing automation identifiers on affected surfaces remain present.
- **SC-009**: The solution builds, all existing suites pass with zero assertion changes, and the formatter gate is clean.

## Assumptions

- This feature **owns** the client-wide design system: the icon size scale and one-glyph-per-concept rule, the colour-token taxonomy, the removal of opacity-based emphasis, and their formalisation as shared styles plus enforced checks. Work on the same surfaces recorded under feature `022` is limited to that feature's own scope (shell spacing, navigation content, search removal).
- The component library already referenced by the desktop client remains the target; no competing UI library or font is introduced.
- Supporting description lines use plain, factual product copy kept short (one line) — no new claims about behaviour.
- Changes are confined to the desktop client's presentation layer and its design documentation.
- Target remains Windows 10 1809+ / Windows 11 desktop, single-tenant and offline-first.

## Dependencies

- The desktop client's existing component-library packages (already referenced).
- The shared client behavior layer for existing bindings, commands, and automation identifiers.
- `specs/022-gallery-design-language/` for the established surfaces and frozen identifiers.

## Out of Scope

- New product features, workflows, or copy beyond short supporting lines and empty-state text.
- Changes to the shared client behavior layer, the workstation API, or any contract.
- Adding imagery, illustrations, or fonts that require network access or user-installed fonts.
- Re-theming the component library itself or shipping a custom theme.
