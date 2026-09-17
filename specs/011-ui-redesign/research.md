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
- **⚠ New dependency**: SettingsCard lives in NuGet `CommunityToolkit.WinUI.Controls.SettingsControls` (`xmlns:controls="using:CommunityToolkit.WinUI.Controls"`), NOT inbox. Plan said "no new packages" — amend: one MIT Toolkit package, compile-time bundled, offline-safe (Constitution IV unaffected). Alternative (hand-rolled cards) rejected: non-standard, more XAML, fails the PowerToys-reference goal.
- **NavigationView responsive pane** (`gallery-navigationview-3`): `AdaptiveTrigger MinWindowWidth={x:Bind nvSample.CompactModeThresholdWidth}` → `PaneDisplayMode=Top`. Adopt for narrow-window reflow (FR: ≤720px). Keep `SelectionChanged` (not `ItemInvoked`) — our shell already does this correctly.
- **Expander gotcha** (`gallery-expander-1`): `IsExpanded` TwoWay needs explicit `x:Bind Mode=TwoWay`.
- **External refs** (web, 2026-09-17): Dev Home (`microsoft/devhome`, WinUI 3, discontinued May 2025 — reference only); PowerToys Settings (SettingsCard authority); WinUI 3 Gallery app (control catalog); Galileo (Mica + virtualized lists); SmrtDoodle (`Themes/` incl. HC dictionaries — model for T004).
- **Chat**: no reference implementation anywhere in Gallery/Toolkit — bespoke control is the right call.

## T003 — Upload nav-vs-dialog ruling

**Ruled**: keep the `ContentDialog` (`UploadDialog`), add a nav item that opens it. Satisfies FR-004 (Upload listed in nav) without resurrecting the removed route (008 FR-007). Reversible at T007 if opposed.
