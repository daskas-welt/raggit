# Data Model: WPF-UI Modernization

**Feature**: `021-wpfui-modernization` | **Date**: 2026-09-29 | **Plan**: [plan.md](plan.md)

This feature introduces no persisted data and no API contract. Its "model" is the catalog the audit
produces and enforces: control families, theme tokens, icon mappings, and the frozen automation-ID
set. Baselines below are the measured pre-change counts in `src/RAGGit.Client.WPF`.

## Entity: Control Mapping Entry

One row per control family. Drives FR-001, FR-010, SC-002, and the mapping document deliverable.

| Field | Description |
|-------|-------------|
| `Family` | Control family (e.g. Button, TextBlock, ListView) |
| `BaselineCount` | Measured raw (non-`ui:`) occurrences before the change |
| `Disposition` | `Convert` (use `ui:` subclass) or `StyleOnly` (no `ui:` subclass; keep native, rely on implicit style) or `Structural` (layout, kept) |
| `TargetType` | The `ui:` type or "native + implicit style" |
| `AutomationRisk` | ID/binding contracts that must survive |
| `Rationale` | Why this disposition (required for every `StyleOnly`/`Structural` exception) |

### Catalog (initial)

| Family | Baseline | Disposition | TargetType | Notes |
|--------|---------:|-------------|------------|-------|
| `Button` | 9 | Convert | `ui:Button` | Use `Appearance` for primary/secondary; `Icon` for icon buttons |
| `TextBlock` | 127 | Convert | `ui:TextBlock` | `Typography` replaces literal `FontSize`; plain allowed in templates only, as exception |
| `ListView` | 12 | Convert | `ui:ListView` | Must remain virtualized (FR-012) |
| `ListBox` | 9 | StyleOnly | native + implicit style | No `ui:ListBox` in 4.3.0 |
| `ProgressBar` | 8 | Convert/StyleOnly | `ui:ProgressRing` (indeterminate) / native (determinate) | Indeterminate → ring; no `ui:ProgressBar` exists |
| `ComboBox` | 2 | StyleOnly | native + implicit style | No `ui:ComboBox` in 4.3.0; includes `PageSizeCombo`, `NewRoleCombo` |
| `CheckBox` | 1 | StyleOnly | native + implicit style | `ResetMustChangeCheck` |
| `RadioButton` | 2 | StyleOnly | native + implicit style | `LightThemeRadio`, `DarkThemeRadio` |
| `Expander` | 2 | Convert/StyleOnly | `ui:CardExpander` where a card fits, else native | No `ui:Expander` |
| `Border` | 12 | Structural | native | Theme brushes only; no literals |
| `Separator` | 1 | StyleOnly | native + implicit style | |
| `Ellipse` | 1 | Convert | `ui:SymbolIcon` | `StatusChip` dot → status glyph |
| `ItemsControl` | 11 | Structural | native | Templating/`ItemsPanel`; keep |
| `ScrollViewer` | 4 | Structural | native | MUST NOT wrap a virtualizing collection (FR-012) |
| `TextBox` | 0 raw | Convert (done) | `ui:TextBox` | 4 already converted |
| `PasswordBox` | 0 raw | Convert (done) | `ui:PasswordBox` | 3 already converted |
| `Grid`/`StackPanel`/`WrapPanel`/`ColumnDefinition`/`RowDefinition` | — | Structural | native | Layout primitives; kept |

**Validation rules**: every non-`Structural` family is `Convert` or carries a `Rationale`; SC-002 passes
only when `Convert` families are at 0 raw and every `StyleOnly`/`Structural` row has a rationale.

## Entity: Theme Token

Replaces every literal color. Drives FR-002, FR-003, SC-001.

| Token (library resource) | Replaces (baseline literal) | Usage site |
|--------------------------|-----------------------------|------------|
| `AccentFillColorDefaultBrush` | `#0078D4` | `ChatControl.xaml:44,133`; `PaginationFooterControl.xaml:14`; `UploadDialog.xaml:100` |
| `AccentTextFillColorPrimaryBrush` | `White` foreground on accent | `ChatControl` bubble text; pager current-page label |
| `SystemFillColorCriticalBrush` | `#D13438`, `#D13438` brush | `UploadDialog.xaml:198`; `StatusChip` Error tone |
| `SystemFillColorSuccessBrush` | `#107C10` brush | `StatusChip` Positive tone |
| `SystemFillColorNeutralBrush` | `#605E5C` brush | `StatusChip` Neutral tone |
| `SystemFillColorAttentionBrush` / `AccentFillColorDefaultBrush` | `#0078D4` brush | `StatusChip` InProgress tone |
| `TextFillColorPrimaryBrush` | (existing) | primary text |
| `TextFillColorSecondaryBrush` | (existing) | secondary text |

**Validation rules**: zero `#[0-9A-Fa-f]{6,8}` and zero `Color.FromRgb` in `RAGGit.Client.WPF` after the
change; all references use `{ui:ThemeResource …}` / `{DynamicResource …}`.

## Entity: Icon Mapping

Drives FR-004, FR-005, SC-003, SC-006.

| Surface | Baseline (literal) | Target (`SymbolRegular`) | Automation |
|---------|--------------------|--------------------------|------------|
| Pager first | `«` text | `ChevronDoubleLeft24` | `FirstPageButton` (keep) |
| Pager previous | `‹` text | `ChevronLeft24` | `PreviousPageButton` (keep) |
| Pager next | `›` text | `ChevronRight24` | `NextPageButton` (keep) |
| Pager last | `»` text | `ChevronDoubleRight24` | `LastPageButton` (keep) |
| Status chip | `Ellipse` + text | `SymbolIcon` (status glyph) + label | `components:StatusChip` (keep) |
| Nav rail | `SymbolRegular.*24` | unchanged (already correct) | `NavDashboard`, etc. (keep) |
| Row/action icons | `ui:SymbolIcon`/`ui:Button Icon` | unchanged (already correct) | `CopyMessageButton`, `UploadButton`, … (keep) |

**Validation rules**: no `Button.Content` or `TextBlock.Text` may be a bare icon glyph; every icon-only
interactive element has `AutomationProperties.Name` + `ToolTip`.

## Entity: Automation ID Set (frozen contract)

Drives FR-008, FR-009, SC-005. These values MUST NOT change; new controls MUST add new IDs.

```text
AddFilesButton, AdminStatusBar, AskAgainButton, BackButton, ChatInputBox,
ConnectionStatusValue, CopyAnswerButton, CopyCitationButton, CopyMessageButton,
CopyPromptButton, CreateUserButton, DarkThemeRadio, DashboardAskButton,
DashboardLibraryButton, DashboardMetricDocuments, DashboardMetricQueries,
DashboardProfileCard, DashboardRefreshButton, DashboardRetryButton,
DashboardStatusBar, FirstPageButton, LastPageButton, LibraryRetryButton,
LightThemeRadio, LoadMoreButton, NewDisplayNameBox, NewPasswordBox, NewRoleCombo,
NewUsernameBox, NextPageButton, PageSizeCombo, PasswordBox, PreviousPageButton,
RaggitBrandLabel, RefreshButton, RefreshUsersButton, ResetMustChangeCheck,
ResetPasswordBox, ResetPasswordButton, SendQuestionButton, SignedInAsValue,
SignInButton, SignOutButton, SuggestionChipButton, SuggestionChips, UploadButton,
UploadDropArea, UploadQueueList, UsernameBox, UsersList, ViewQueryButton,
WorkstationUrlValue
```

Plus three bound IDs whose values come from view models (not literals):
`{Binding MessageCitationsAutomationId}`, `{Binding SourcesAutomationId}`,
`{Binding RemoveAutomationId, Mode=OneWay}`.

**Validation rules**: the post-change ID set is a superset of this list; none of these values is removed
or renamed.

## State Transitions

Not applicable — the feature introduces no runtime state machine. Theme state is owned by the library's
`ApplicationThemeManager`; the only transition the client initiates is applying the system theme at
startup (Light/Dark/High Contrast), with Light/Dark as an explicit user override in Settings.
