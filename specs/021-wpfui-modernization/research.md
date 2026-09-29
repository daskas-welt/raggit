# Research: WPF-UI Modernization

**Feature**: `021-wpfui-modernization` | **Date**: 2026-09-29 | **Plan**: [plan.md](plan.md)

This records the decisions that resolve the Technical Context. All findings were verified against the
installed `Wpf.Ui.dll` 4.3.0 (public control types from the compiled XML docs, brush-key and font
strings from the assembly) and the library documentation surfaced via Context7 (`/lepoco/wpfui`),
not from memory.

## Decision: Treat WPF-UI as already adopted; modernize rather than introduce

**Rationale**: The client already references `WPF-UI` and `WPF-UI.DependencyInjection` 4.3.0, merges
`ui:ThemesDictionary` + `ui:ControlsDictionary` (`App.xaml`), wires `ContentDialogService`,
`SnackbarService`, and `NavigationViewPageProvider` (`App.xaml.cs:111-115`), and the shell is a
`ui:FluentWindow` with `ui:TitleBar`, `ui:NavigationView`, `ui:ContentDialogHost`, and
`ui:SnackbarPresenter` (`MainWindow.xaml`). The feature is a completion/audit pass, not a migration.

**Alternatives considered**:
- Greenfield rewrite of the client onto the library: rejected — disproportionate risk for a presentation-only outcome; the shell already works.
- Introduce a second UI library: rejected — duplicates theming and violates Simplicity.

## Decision: Prefer `ui:` subclasses where they exist; rely on implicit styles elsewhere

**Rationale**: WPF-UI 4.3.0 ships `ui:` subclasses for a subset of controls and, via
`ui:ControlsDictionary`, applies implicit Fluent styles to the remaining standard WPF controls.
The audit must prefer the subclass where it adds library properties (`Icon`, `Appearance`,
typography) and otherwise keep the native control and record the exception. Verified `ui:` types in
4.3.0 include: `Button`, `TextBlock`, `TextBox`, `PasswordBox`, `ListView`, `GridView`, `DataGrid`,
`ToggleSwitch`, `NumberBox`, `AutoSuggestBox`, `Card`, `CardExpander`, `InfoBar`, `ProgressRing`,
`SymbolIcon`, `FontIcon`, `NavigationView`, `ContentDialog`, `Snackbar`, `FluentWindow`, `Badge`,
`BreadcrumbBar`, `TabView`.
No `ui:` subclass exists for `ComboBox`, `CheckBox`, `RadioButton`, `Expander`, `ListBox`,
`ProgressBar`, or `Slider` — these keep their native type and are themed by the implicit dictionary.

**Alternatives considered**:
- Wrap or re-template `ComboBox`/`CheckBox`/`RadioButton` locally: rejected — re-implements theming the library already applies, violating Simplicity.
- Force every control to a `ui:` type even where none exists: not possible in 4.3.0.

## Decision: Icons use the bundled Fluent icon set (`SymbolIcon` + `SymbolRegular`)

**Rationale**: `Wpf.Ui.dll` embeds `FluentSystemIcons` (18 string references) and only references the
OS `Segoe Fluent Icons`/`SegoeFluentIcons` font once. `SymbolIcon`/`SymbolRegular` therefore render
from the font shipped with the library and work on Windows 10 1809+ (the client's target floor)
without any user-installed font or EULA handling, satisfying FR-004/FR-005 and SC-006. The nav rail
(`MainWindowViewModel.cs:50-66`) and several buttons already use `SymbolRegular`, so this also keeps
the icon language consistent. The pager's literal `« ‹ › »` become
`ChevronDoubleLeft24`/`ChevronLeft24`/`ChevronRight24`/`ChevronDoubleRight24`.

**Alternatives considered**:
- `ui:FontIcon` with the OS `Segoe Fluent Icons` font: rejected as the default — the font is absent on
  Windows 10; per the library's own note it must be manually added for older Windows, adding EULA
  and packaging burden for no visual gain. Reserved for a glyph the bundled set lacks.
- Keep literal text glyphs: rejected — breaks the icon language and can vanish in High Contrast.

## Decision: Replace all color literals with the library's semantic theme brushes

**Rationale**: `Wpf.Ui.dll` exposes the semantic keys needed, verified present: `SystemFillColorSuccessBrush`,
`SystemFillColorCriticalBrush`, `SystemFillColorCautionBrush`, `SystemFillColorAttentionBrush`,
`SystemFillColorNeutralBrush`, `SystemFillColorSolidNeutralBrush`, `TextFillColorPrimaryBrush`,
`TextFillColorSecondaryBrush`, `AccentFillColorDefaultBrush`, `AccentTextFillColorPrimaryBrush`,
`ControlFillColorDefaultBrush`, `ControlStrokeColorDefaultBrush`, `SubtleFillColorSecondaryBrush`.
Mapping: chat/user bubble `#0078D4` → `AccentFillColorDefaultBrush` (text `AccentTextFillColorPrimaryBrush`);
pager current page `#0078D4` → `AccentFillColorDefaultBrush` or `ui:Button Appearance="Primary"`;
upload dialog border `#0078D4` → `AccentFillColorDefaultBrush`; upload error `#D13438` →
`SystemFillColorCriticalBrush`; `StatusChip` tones → Positive=`SystemFillColorSuccessBrush`,
InProgress=`AccentFillColorDefaultBrush`, Error=`SystemFillColorCriticalBrush`,
Neutral=`SystemFillColorNeutralBrush`.

**Alternatives considered**:
- Keep literal hex and add local theme dictionaries: rejected — duplicates the library's palette and drifts.
- Hard-code a Light+Dark pair per literal: rejected — misses High Contrast, which the library handles automatically.

## Decision: `StatusChip` resolves semantic brushes at runtime

**Rationale**: `StatusChip.xaml.cs:16-27` stores four frozen `SolidColorBrush` literals. Because the
tone is computed in code, the fix is to resolve the semantic resources through the application
(`TryFindResource` on `Application.Current`) keyed by tone, and keep the existing `DotBrush`/`TextBrush`
dependency properties and `DocumentStatusPresentation.ToneFor` contract intact (FR-011). The dot may
become a `ui:SymbolIcon` status glyph to satisfy FR-004, with the label text preserved.

**Alternatives considered**:
- Move tone→brush mapping into XAML with data triggers: workable but spreads the mapping across the view; runtime lookup keeps one source of truth and the tested Core mapping.

## Decision: Follow the system theme at startup; keep explicit selection

**Rationale**: `MainWindow.xaml.cs:52` already calls `SystemThemeWatcher.Watch(this)`, but `App.xaml`
declares `<ui:ThemesDictionary Theme="Light" />`, which fixes the initial dictionary. Applying
`ApplicationThemeManager.ApplySystemTheme()` during startup makes the default follow Windows
(FR-003), while `SystemThemeWatcher` keeps live system changes and the library auto-loads the
`HC1`/`HC2`/`HCBlack`/`HCWhite` dictionaries for High Contrast. The existing Light/Dark radios in
`SettingsPage` remain a deliberate override.

**Alternatives considered**:
- Remove `ThemesDictionary Theme`: rejected — the library expects an initial theme and design-time tooling relies on it.
- Persist and restore the last selection across launches: rejected — out of scope (new persistence), and it would conflict with "follow the system by default".

## Decision: Typography comes from the library's text styles

**Rationale**: 127 plain `TextBlock` with ad-hoc `FontSize` values are the largest inconsistency
surface. `Wpf.Ui.Controls.TextBlock` (present in 4.3.0) exposes `Typography` (`FontTypography`):
Body, Caption, Subtitle, Title, etc. Adopting `ui:TextBlock` + `Typography` satisfies FR-007 and
removes literal sizes; plain `TextBlock` remains acceptable only inside templates where a `ui:`
element would break an existing binding or automation contract, recorded as an exception.

**Alternatives considered**:
- Define local `Style` resources for text: rejected — duplicates the library typography scale.
- Leave `FontSize` literals: rejected — fails FR-007/SC-004 consistency.

## Decision: Lists stay virtualized; no `ScrollViewer` wraps a virtualizing collection

**Rationale**: 12 `ListView`, 9 `ListBox`, 11 `ItemsControl`, and 4 `ScrollViewer` require an audit
because wrapping a virtualizing collection in a `ScrollViewer` disables virtualization. Converting to
`ui:ListView`/`ui:GridView` keeps the library's virtualization behavior. This enforces FR-012 and
protects the performance goal.

**Alternatives considered**:
- Convert lists to non-virtualizing panels for simpler styling: rejected — regresses scroll performance on large libraries/histories.

## Decision: Preserve Automation IDs as a compatibility contract

**Rationale**: UI automation consumers and the project's UI-validation convention depend on stable
`AutomationProperties.AutomationId` values. The audit captured the current ID set and freezes it
(see `contracts/ui-contracts.md`). New interactive elements get new IDs; existing IDs are never
renamed or repurposed (FR-008, FR-009).

**Alternatives considered**:
- Rename IDs to match new control names: rejected — breaks existing validation and automation.

## Design grounding sources

- Installed `Wpf.Ui.dll` 4.3.0: public control type list (XML docs) and brush/font string keys (assembly scan).
- WPF-UI docs via Context7 (`/lepoco/wpfui`): SymbolIcon/FontIcon, icons/`Segoe Fluent Icons`, themes, `ApplicationThemeManager`, `SystemThemeWatcher`, High Contrast dictionaries.
- In-repo baselines: `specs/011-ui-redesign` (theme/semantic-resource stance, no `ScrollViewer`-wrapped lists), `specs/012-fonticon-glyphs` (Segoe Fluent Icons with `Symbol`-enum fallback for 1809, icon accessible names).
- `src/RAGGit.Client.WPF` audit: per-file control inventory, 5 XAML color literals, 4 `StatusChip` brush literals, and the frozen Automation ID set.
