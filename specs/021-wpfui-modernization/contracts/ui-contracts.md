# UI Contracts: WPF-UI Modernization

**Feature**: `021-wpfui-modernization` | **Date**: 2026-09-29 | **Plan**: [plan.md](../plan.md)

This is a desktop application with no external API, CLI, or wire contract. Its observable contracts
are the presentation invariants below. They are checked by the audit + manual verification in
[quickstart.md](../quickstart.md); no schema file is generated.

## C1 — Control Mapping Contract (FR-001, FR-010, SC-002)

Every interactive control family in `RAGGit.Client.WPF` MUST be in exactly one state:

1. **Converted** — uses the library's `ui:` subclass (`ui:Button`, `ui:TextBlock`, `ui:ListView`,
   `ui:ProgressRing`, `ui:TextBox`, `ui:PasswordBox`, `ui:Card`, `ui:CardExpander`, `ui:InfoBar`,
   `ui:SymbolIcon`, …).
2. **Implicitly styled** — the library provides no `ui:` subclass (`ComboBox`, `CheckBox`,
   `RadioButton`, `ListBox`, `ProgressBar`, `Slider`, `Separator`); the native control is kept and
   relies on `ui:ControlsDictionary`. A rationale MUST be recorded.
3. **Structural** — a layout primitive (`Grid`, `StackPanel`, `WrapPanel`, `Border`, `ItemsControl`,
   `ScrollViewer`) that is intentionally not restyled. A rationale MUST be recorded.

**Prohibited**: a control family left with neither a conversion nor a recorded rationale; a `ui:`
element that breaks an existing binding, template, or automation contract.

## C2 — Theme Token Contract (FR-002, FR-003, SC-001)

1. Zero hard-coded color literals (`#RRGGBB(AA)`, `Colors.*`, `Color.FromRgb`) in `RAGGit.Client.WPF`
   XAML or code-behind.
2. Every color reference is `{ui:ThemeResource …}` or `{DynamicResource …}` so theme changes propagate.
3. Light, Dark, and High Contrast all render legible text, borders, and focus visuals; High Contrast is
   supplied by the library (`HC1`/`HC2`/`HCBlack`/`HCWhite`) — the client MUST NOT force feature colors
   onto it.
4. Startup follows the Windows theme (`ApplicationThemeManager.ApplySystemTheme()`); live changes are
   watched (`SystemThemeWatcher`); Light/Dark remain explicit Settings overrides.

## C3 — Icon Contract (FR-004, FR-005, SC-003, SC-006)

1. Action/status icons render via `ui:SymbolIcon` + `SymbolRegular` (library-bundled Fluent icon font).
2. No interactive `Button.Content` or `TextBlock.Text` is a bare glyph character.
3. Icons recolor from theme `Foreground`; no literal fills.
4. Icons render on Windows 10 1809+ with no user-installed font.
5. An icon-only interactive control MUST carry `AutomationProperties.Name` and a `ToolTip` with the
   same meaning.

## C4 — Automation ID Contract (FR-008, FR-009, SC-005)

1. The frozen ID set in [data-model.md](../data-model.md) MUST remain intact: none removed, renamed,
   or repurposed.
2. Every new interactive element added by this feature MUST declare a stable
   `AutomationProperties.AutomationId`.
3. The three view-model-bound IDs (`MessageCitationsAutomationId`, `SourcesAutomationId`,
   `RemoveAutomationId`) MUST keep their binding paths.

## C5 — Keyboard & Focus Contract (FR-009, FR-013, SC-005)

1. Every primary action is reachable by keyboard with a visible focus indicator.
2. Opening and closing a `ui:ContentDialog` returns focus to the invoking control (library behavior;
   verified, not reimplemented).
3. At the minimum window size (800×600 from `MainWindow`), all primary content is reachable and
   unclipped.

## C6 — Behavior-Freeze Contract (FR-011, FR-012)

1. `RAGGit.Client.Core` ViewModels, API clients, `RAGGit.Workstation.Api`, and
   `specs/*/contracts/api.yaml` are read-only for this feature.
2. No query, upload, citation, identity, or offline behavior changes.
3. No `ScrollViewer` wraps a virtualizing collection; lists stay virtualized.

## C7 — Typography Contract (FR-007, SC-004)

1. Text uses the library's typography (`ui:TextBlock` + `Typography`); ad-hoc `FontSize` literals are
   removed except inside templates where a `ui:` element would break a contract (recorded exception).
