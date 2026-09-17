# Feature Specification: FontIcon Glyphs (Replace Converter-Driven Text Glyphs)

**Feature Branch**: `012-fonticon-glyphs`

**Created**: 2026-09-17

**Status**: Draft

**Input**: User description: "replace converter-driven glyphs to FontIcon"

**Constitution**: v1.3.0 — no amendment needed (presentation-only change in the WinUI client; Core behavior, APIs, and contracts untouched).

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Admin status glyphs render as proper icons (Priority: P1)

An admin viewing People Management sees the per-user Active toggle and Locked indicator as crisp theme-aware icons (checkmark / cross / lock) instead of text characters, in every theme including High Contrast.

**Why this priority**: This is the only surface still using text glyphs after the 011 reskin (Library row buttons and chat send already use `SymbolIcon`); it completes the icon story and fixes High Contrast, where text glyphs can vanish.

**Independent Test**: Open Admin as admin in Light, Dark, and Contrast themes → active toggle shows check/cross icon, locked users show lock icon; all remain visible and correctly colored in each theme.

**Acceptance Scenarios**:

1. **Given** an active user row, **When** viewed in any theme, **Then** a checkmark icon is shown in the Active column.
2. **Given** an inactive user row, **When** viewed in any theme, **Then** a cross icon is shown in the Active column.
3. **Given** a locked-out user row, **When** viewed in any theme, **Then** a lock icon alone is shown in the Locked column (icon-only with tooltip and accessible name).
4. **Given** an unlocked user row, **When** viewed in any theme, **Then** the Locked cell is empty (clarified 2026-09-17: lock presence alone carries the meaning).
4. **Given** a Contrast theme, **When** the Admin page renders, **Then** all three icons remain visible (system-driven foreground, never hard-coded fills).

---

### User Story 2 — Icons are announced correctly by screen readers (Priority: P2)

A screen-reader user navigating the Admin list hears the meaning of each status icon (e.g. "Active", "Locked"), never an unpronounceable glyph.

**Why this priority**: Text glyphs expose raw characters to assistive tech; proper icon slots pair with accessible names — the same standard T008 applied to row buttons.

**Independent Test**: Walk the Admin list with a screen reader → each icon announces its meaning; toggle buttons expose name + state.

**Acceptance Scenarios**:

1. **Given** a screen reader on a user row, **When** focus reaches the Active toggle, **Then** its name and current state are announced.
2. **Given** a screen reader on a user row, **When** focus reaches the Locked indicator, **Then** "Locked" (or nothing meaningful for unlocked) is announced — never a raw character.

---

### Edge Cases

- Unlocked users (currently a "—" dash placeholder): keep a neutral treatment — icon or empty cell must not confuse with the lock icon.
- Converters keep their existing contracts for any other consumers (no behavior change outside the Admin list).
- If an icon font glyph is unavailable on Windows 10 1809, fall back to a `Symbol`-enumeration icon that exists on all supported versions.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The Active toggle MUST remain a `Button` showing a `FontIcon` (or `SymbolIcon` where the enumeration covers it) instead of converter-emitted text characters (clarified 2026-09-17: keep Button pattern, swap glyph for icon).
- **FR-002**: The Locked indicator MUST be an icon-only `FontIcon` (or `SymbolIcon`) with tooltip and accessible name instead of converter-emitted text characters (clarified 2026-09-17); unlocked rows show an empty cell.
- **FR-003**: Icons MUST recolor via `Foreground` (theme-driven) — no hard-coded fills — so Contrast themes render them correctly.
- **FR-004**: Icon-only interactive elements MUST carry accessible names (and tooltips), matching the T008 standard.
- **FR-005**: The underlying true/false meaning MUST NOT change — converters keep mapping active→checkmark, inactive→cross, locked→lock.
- **FR-006**: No other consumers of the touched converters may change behavior; Core and test suites stay green with zero assertion changes.

### Key Entities

- **Status glyphs**: Active check/cross + Locked lock — the three converter-driven visuals, now theme-aware icons.
- **Glyph converters**: `ActiveGlyphConverter`, `LockedGlyphConverter` (`src/RAGGit.Client.WinUI/Converters/ViewConverters.cs`) — formatting layer to adapt, behavior preserved.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Zero text-glyph status indicators remain in the WinUI client (all status visuals are icon elements).
- **SC-002**: All three icons visible and correctly colored in Light, Dark, and Contrast themes (100% of theme checks).
- **SC-003**: Screen-reader walkthrough announces meaning (never raw characters) for 100% of status icons.
- **SC-004**: All existing unit/contract/integration tests pass with zero assertion changes.

## Assumptions

- Unlocked-row treatment clarified 2026-09-17 via `/speckit.clarify`: empty cell (not dash, not unlocked icon).
- Active control clarified 2026-09-17: keep Button-with-icon pattern (no ToggleSwitch migration).
- Locked column clarified 2026-09-17: icon-only with tooltip + accessible name.
- Exact glyph codepoints are chosen at plan time from the Segoe Fluent Icons table (verified, never guessed) with `Symbol`-enum fallback for 1809 coverage.
- Footer pager chevrons (« ‹ › ») are out of scope (separate deferred item).
- Implementation lives in WinUI XAML + its converters only; `RAGGit.Client.Core` and tests are read-only.
