# Control Mapping: WPF-UI Modernization

**Feature**: `021-wpfui-modernization` | **Date**: 2026-09-29 | **Plan**: [plan.md](plan.md)

Audit artifact for FR-001/FR-010 and SC-002. It records the disposition of every control
family in `src/RAGGit.Client.WPF` against the referenced library (WPF-UI 4.3.0) and the
rationale for every exception. Validated by the C1/C2/C3/C4/C6 checks in
[quickstart.md](quickstart.md).

## Conventions (T003)

- `ui:ThemesDictionary` (theme) and `ui:ControlsDictionary` (implicit control styles) are
  merged at application level in [`App.xaml`](../../src/RAGGit.Client.WPF/App.xaml); the
  library's semantic resources are therefore available to every view.
- Color and brush references use `{ui:ThemeResource …}` or `{DynamicResource …}` so theme
  changes (Light/Dark/High Contrast) propagate. No color literal remains (C2).
- Text uses `ui:TextBlock` + `Typography` (`FontTypography`) instead of ad-hoc `FontSize`
  (C7). Plain `TextBlock` is retained only inside templates/controls where a `ui:` element
  would break a binding or an automation contract, recorded below.
- The app follows the system theme at startup (`ApplicationThemeManager.ApplySystemTheme()`
  in `App.xaml.cs`, live changes via `SystemThemeWatcher`) with `Light`/`Dark` as explicit
  Settings overrides (C2.4).

## Catalog

`Baseline` is the measured pre-change raw (non-`ui:`) occurrence count; `Final` is the raw
count after this feature. `Convert` families end at 0 raw; `StyleOnly`/`Structural` families
carry a rationale.

| Family | Baseline | Final | Disposition | TargetType | AutomationRisk | Rationale |
|--------|---------:|------:|-------------|------------|----------------|-----------|
| `Button` | 9 | 0 | Convert | `ui:Button` (`Appearance` for primary/transparent) | Page/nav/send IDs, `ToolTip`/`Name` | Library subclass adds `Appearance`, `Icon`, typography; every ID/tooltip preserved. |
| `TextBlock` | 127 | 0 | Convert | `ui:TextBlock` + `Typography` | Value IDs (`WorkstationUrlValue`, …) | Library typography removes ad-hoc `FontSize`; IDs preserved. |
| `TextBox` | 4 (done) | 0 | Convert | `ui:TextBox` | `UsernameBox`, `ChatInputBox`, … | Already converted; placeholder/typography from library. |
| `PasswordBox` | 3 (done) | 0 | Convert | `ui:PasswordBox` | `PasswordBox`, `NewPasswordBox`, `ResetPasswordBox` | Already converted. |
| `ListView` | 12 | 0 | Convert | `ui:ListView` | `UsersList`, `UploadQueueList`, list rows | Library list keeps virtualization (FR-012); row templates/bindings unchanged. |
| `GridView` / `DataGrid` | 0 | 0 | Convert (n/a) | `ui:GridView` / `ui:DataGrid` | — | No occurrence; listed for completeness. |
| `ProgressRing` | 0 | 8 (ui) | Convert | `ui:ProgressRing` | — | Indeterminate loading states use the library ring. |
| `SymbolIcon` | 7 (done) | 12 | Convert | `ui:SymbolIcon` + `SymbolRegular` | Icon-only IDs + `Name`/`ToolTip` | Bundled Fluent icon font; recolors from theme foreground (FR-004/005). |
| `Ellipse` | 1 | 0 | Convert | `ui:SymbolIcon` | `components:StatusChip` usage | Status dot became a themed status glyph; label text preserved. |
| `Card` / `CardExpander` | 29 (done) | 29 | Convert | `ui:Card` | Card-hosted IDs | Already converted; used as the settings/row container. |
| `InfoBar` | 4 (done) | 4 | Convert | `ui:InfoBar` | Status surfaces | Already converted; used for outcome banners. |
| `ListBox` | 9 | 3 | **StyleOnly** | native + implicit `ControlsDictionary` style | `SourcesAutomationId`, message lists | WPF-UI 4.3.0 ships **no** `ui:ListBox`; the native control is themed by the implicit dictionary. Used for chat/citation selection lists, not for tabular data. Splitting these into `ui:ListView` would change selection semantics (FR-011). |
| `ComboBox` | 2 | 2 | **StyleOnly** | native + implicit style | `PageSizeCombo`, `NewRoleCombo` | No `ui:ComboBox` in 4.3.0; implicit Fluent style applied. |
| `CheckBox` | 1 | 1 | **StyleOnly** | native + implicit style | `ResetMustChangeCheck` | No `ui:CheckBox` in 4.3.0. |
| `RadioButton` | 2 | 2 | **StyleOnly** | native + implicit style | `LightThemeRadio`, `DarkThemeRadio` | No `ui:RadioButton` in 4.3.0. |
| `ProgressBar` | 8 | 2 | **StyleOnly** (determinate) | native + implicit style | Upload progress `Name` | No `ui:ProgressBar` in 4.3.0; `ui:ProgressRing` cannot express a determinate `Value`/`Maximum` (and the per-file bar toggles `IsIndeterminate` while indexing). Converting would change upload-state behavior (FR-011). Indeterminate loaders were converted to `ui:ProgressRing`. |
| `Expander` | 2 | 1 | **StyleOnly** | native + implicit style | `SourcesAutomationId` (`ChatControl`) | No `ui:Expander` in 4.3.0. The remaining instance is the inline “Sources” expander inside a chat bubble, where `ui:CardExpander` chrome would nest a card inside a bubble; the implicit style is correct. |
| `Border` | 12 | 12 | **Structural** | native | Status surfaces, bubbles, panes | Layout/visual primitive; carries theme brushes only, no literals. |
| `ItemsControl` | 11 | 11 | **Structural** | native | `DocumentsList`, nav/stat items | Templating + `ItemsPanel` layout; not a selectable control. Non-virtualizing by design and not scroll-owner-wrapped (C6). |
| `ScrollViewer` | 4 | 4 | **Structural** | native | — | Page-level scrolling for panels/forms/`ItemsControl`; none directly wraps a virtualizing `ListView`/`ListBox`/`GridView`/`DataGrid` (C6/FR-012). |
| `Separator` | 1 | 1 | **StyleOnly** | native + implicit style | `AdminUsersPage` header/rows | No `ui:` subclass; implicit style. |
| `Grid` / `StackPanel` / `WrapPanel` / `ColumnDefinition` / `RowDefinition` / `Path` / `Rectangle` | — | — | **Structural** | native | — | Layout/decoration primitives; kept. |

**Validation rules**: every non-`Structural` family is `Convert` or carries a `Rationale`;
`Convert` families sit at 0 raw occurrences; C1 passes when this table is complete.

## Theme token mapping (final)

| Library resource | Replaces (baseline literal) | Site |
|------------------|-----------------------------|------|
| `AccentFillColorDefaultBrush` | `#0078D4` | Chat bubbles, citation ordinal, upload drop border, pager current page (`Appearance="Primary"`), `StatusChip` InProgress |
| `AccentTextFillColorPrimaryBrush` | `White` on accent | Chat bubble text, citation ordinal, pager current page |
| `SystemFillColorCriticalBrush` | `#D13438` | Upload dialog error, `StatusChip` Error |
| `SystemFillColorSuccessBrush` | `#107C10` | `StatusChip` Positive |
| `SystemFillColorNeutralBrush` | `#605E5C` | `StatusChip` Neutral |
| `TextFillColorPrimaryBrush` / `TextFillColorSecondaryBrush` | — | Primary/secondary text |

`StatusChip` resolves the semantic brush by tone through
`Application.Current.Resources[...]` (code-behind) and recolors the glyph/label through
theme-resource `DataTrigger`s on `ToneName` — no frozen `SolidColorBrush` literal remains.

## Icon mapping (final)

| Surface | Baseline | Final | Automation |
|---------|----------|-------|------------|
| Pager first | `«` text | `ui:SymbolIcon ChevronDoubleLeft24` | `FirstPageButton` + `Name`/`ToolTip` |
| Pager previous | `‹` text | `ui:SymbolIcon ChevronLeft24` | `PreviousPageButton` + `Name`/`ToolTip` |
| Pager next | `›` text | `ui:SymbolIcon ChevronRight24` | `NextPageButton` + `Name`/`ToolTip` |
| Pager last | `»` text | `ui:SymbolIcon ChevronDoubleRight24` | `LastPageButton` + `Name`/`ToolTip` |
| Status chip | `Ellipse` + text | `ui:SymbolIcon` (per-tone glyph) + label | `components:StatusChip` |
| Nav rail / row icons | `SymbolRegular.*24` | unchanged (already correct) | `Nav*`, `CopyMessageButton`, `UploadButton`, … |

Every icon-only interactive control carries `AutomationProperties.Name` and a matching
`ToolTip` (C3.5).
