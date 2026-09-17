# Research: WinUI Redesign Grounding (T001–T003)

**Date**: 2026-09-17 · **Tool**: `winapp find-ui` (WinApp CLI 0.6.1, Gallery + Toolkit corpus)

## T001 — Scenario IDs per surface

| Surface | Query | Adopted scenario IDs |
|---|---|---|
| Library cards | `dashboard cards` | No dedicated dashboard control — card primitive is `SettingsCard` (toolkit). Layout via `Border` (gallery-border-1) + `Expander` (gallery-expander-1) composition |
| Ask chat | `chat` | **No match** — no canonical WinUI chat sample exists. Keep bespoke `ChatControl`, style to theme (confirms audit approach) |
| Admin settings | `SettingsCard` | toolkit-settingscard-4 (default Header/Icon/Description/Content), toolkit-settingscard-1 (clickable), toolkit-settingscard-5 (icons), toolkit-settingscard-2/-3 (ActionIcon), toolkit-settingscard-6/-8 |
| Shell nav | `NavigationView pane` | gallery-navigationview-3 (responsive pane Top/Left via `AdaptiveTrigger`), gallery-navigationview-1 (default), gallery-navigationview-7 (hierarchical — not needed) |

## T002 — Adopted patterns

- **SettingsCard** (`toolkit-settingscard-4`): `Header` + `HeaderIcon` (`FontIcon` glyph) + `Description` + content (`ComboBox`/`ToggleSwitch`/etc.). Rule: never hand-roll StackPanel+ToggleSwitch settings rows. `SettingsExpander` groups related cards under one collapsible header.
- **SettingsCard verdict (T017, REJECTED)**: `CommunityToolkit.WinUI.Controls.SettingsControls` 8.1 ships Windows assets only under `net8.0-windows10.0.22621` (`18362` is an empty placeholder). Our floor is `17763` (FR-002 / Constitution VII), so ANY toolkit XAML usage crashes the WASDK 1.5 XamlCompiler (bisected to a minimal 1-expander-1-card repro; package referenced-but-unused compiles fine). Raising the floor to 22621 was rejected (breaks Win10 1809 support). Adopted instead: inbox `Expander` sections + `Header`-labelled editors in the same card chrome — accessible, green, zero new packages (plan's "no new packages" stands). Revisit only if the floor ever rises to 22621+.
- **NavigationView responsive pane** (`gallery-navigationview-3`): `AdaptiveTrigger MinWindowWidth={x:Bind nvSample.CompactModeThresholdWidth}` → `PaneDisplayMode=Top`. Adopt for narrow-window reflow (FR: ≤720px). Keep `SelectionChanged` (not `ItemInvoked`) — our shell already does this correctly.
- **Expander gotcha** (`gallery-expander-1`): `IsExpanded` TwoWay needs explicit `x:Bind Mode=TwoWay`.
- **External refs** (web, 2026-09-17): Dev Home (`microsoft/devhome`, WinUI 3, discontinued May 2025 — reference only); PowerToys Settings (SettingsCard authority); WinUI 3 Gallery app (control catalog); Galileo (Mica + virtualized lists); SmrtDoodle (`Themes/` incl. HC dictionaries — model for T004).
- **Chat**: no reference implementation anywhere in Gallery/Toolkit — bespoke control is the right call.

## T003 — Upload nav-vs-dialog ruling

**Ruled**: keep the `ContentDialog` (`UploadDialog`), add a nav item that opens it. Satisfies FR-004 (Upload listed in nav) without resurrecting the removed route (008 FR-007). Reversible at T007 if opposed.
