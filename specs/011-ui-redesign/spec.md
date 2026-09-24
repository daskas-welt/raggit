# Feature Specification: WinUI Redesign (Native Windows 11)

**Feature Branch**: `011-ui-redesign`
**Created**: 2026-09-17
**Status**: Draft
**Input**: Locked plan — full shell + all pages · pure Windows 11 native (Mica, system accent, Segoe UI Variable) · Dev Home as primary anchor · reskin + audit-violation fixes in one pass.
**Constitution**: v1.3.0 — no amendment needed (presentation-only change; Core/API/contracts untouched, still `1.4.0`).

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Foundations look native in every theme (Priority: P1) ★ MVP

The app launches in a content-sized window with an iconed `NavigationView` pane and renders correctly in Light, Dark, and High Contrast from semantic theme brushes. No behavior changes.

**Independent Test**: Fresh launch → window sized to content (not 1024×768 default); switch Light/Dark/Contrast themes → all surfaces themed, no hard-coded colors; keyboard traverses nav.

**Acceptance Scenarios**:
1. **Given** any page, **When** the system theme changes, **Then** all brushes update (no stale `StaticResource` surfaces).
2. **Given** High Contrast, **When** any page renders, **Then** text, borders, and focus visuals remain legible.
3. **Given** app launch, **When** the window appears, **Then** it is sized to content via the DPI-aware rubric, not the OS default.

---

### User Story 2 — Library reads as a native browser (Priority: P1)

The Document Library uses Dev Home card styling with Explorer-like density: themed grid rows, responsive columns, empty state, and pagination footer.

**Independent Test**: Library with 0, 1, and 50+ documents in each theme; narrow window keeps content reachable; keyboard reaches every row action.

---

### User Story 3 — Ask reads as native chat (Priority: P1)

Chat bubbles use theme brushes, citations render without overlapping the error surface, and long conversations stay performant.

**Independent Test**: 20-message conversation in order with citations in all 3 themes; no overlap of citations footer and status text.

---

### User Story 4 — Secondary pages match the shell (Priority: P2)

History, My Docs, and Query Detail use the same card language and virtualize their lists (no `ScrollViewer`-wrapped `ListView`).

**Independent Test**: Each page scrolls smoothly with 100+ rows; empty states themed; keyboard walkthrough clean.

---

### User Story 5 — Admin and Login meet platform conventions (Priority: P2)

Admin uses `SettingsCard`/`SettingsExpander` with proper `Header` labels; Login binds per-keystroke with correct error visibility.

**Independent Test**: Full admin form operable by keyboard + screen reader; login shows errors immediately while typing.

---

### Edge Cases

- Narrow window (≤720px): content reflows, nothing clipped with no route to reach it.
- Screen reader: nav items, cards, and dialogs announced; focus returns after dialog close.
- Legacy "original unavailable" and empty-list states keep working, themed.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: All brushes MUST come from `ThemeDictionaries` (Light/Dark/HighContrast) via `{ThemeResource}` — zero hard-coded color literals in XAML.
- **FR-002**: `MainWindow` MUST size via DPI-aware `AppWindow.Resize` (`GetDpiForWindow`); no `SizeToContent` assumption.
- **FR-003**: Every `NavigationViewItem` MUST carry a `SymbolIcon`; Upload entry-point ruling (nav item opening the dialog vs dialog-only) MUST be recorded before nav work.
- **FR-004**: Glyph/emoji buttons MUST become `SymbolIcon`/`FontIcon`; status surfaces MUST be `InfoBar`; destructive actions MUST confirm via `ContentDialog`.
- **FR-005**: No `ScrollViewer` may wrap a `ListView`/`GridView` (virtualization must hold).
- **FR-006**: `TextBox` two-way bindings MUST use `UpdateSourceTrigger=PropertyChanged`; bool→Visibility MUST go through a converter/function.
- **FR-007**: No behavior change — `RAGGit.Client.Core`, APIs, and contracts are read-only for this feature.
- **FR-008**: Reference grounding (`winapp find-ui` scenario IDs + Dev Home/PowerToys/Gallery patterns) MUST be recorded in `research.md` before XAML changes per surface.

### Key Entities

- **Theme foundation**: `App.xaml` dictionaries + semantic brush names.
- **Shell**: `MainWindow` + `NavigationView` + native backdrops (Mica, system accent).
- **Shared controls**: `ChatControl`, `PaginationFooterControl`, `StatusChip`, converters.

## Success Criteria *(mandatory)*

- **SC-001**: Zero hard-coded color literals in WinUI XAML; 109 `{StaticResource}` theme sites converted.
- **SC-002**: All pages pass Light/Dark/HighContrast visual check + keyboard walkthrough.
- **SC-003**: `dotnet build` + full test suites green with zero assertion changes; `csharpier check` clean.
- **SC-004**: No `ScrollViewer`-wrapped collection controls remain; ≥20-message chat stays smooth.

## Assumptions

- Dev Home (discontinued May 2025) is a visual/code reference only, never a dependency.
- Upload ruling default: keep the dialog, add a nav item that opens it (record otherwise in Phase 0).
- `RAGGit.Client.Core` namespaces stay as-is (T028 rename is out of scope).
- `docs/architecture.puml` regen via PlantUML is a docs follow-up, not a redesign task.
