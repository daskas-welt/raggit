# Research: FontIcon Glyphs (012)

**Date**: 2026-09-17 · **Skill**: `winui-design` (icons reference) · **Refs**: [Icons in Windows apps](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/icons), [Segoe Fluent Icons font table](https://learn.microsoft.com/en-us/windows/apps/design/style/segoe-fluent-icons-font) (dated 2026-09-03, verified this session).

## Decision 1 — Glyph codepoints (verified, not guessed)

| Meaning | Codepoint | Table name | 1809-safe |
|---|---|---|---|
| Active (check) | `E73E` | CheckMark | Yes — shared MDL2/Fluent PUA |
| Inactive (cross) | `E711` | Cancel | Yes — shared MDL2/Fluent PUA |
| Locked (lock) | `E72E` | Lock | Yes — shared MDL2/Fluent PUA |
| Unlocked | `` (empty) | — | n/a (clarified: empty cell) |

- Rationale: all three codepoints sit in PUA E700–E9F9, identical in Segoe MDL2 Assets and Segoe Fluent Icons, so `SymbolThemeFontFamily` renders them on Win11 (Fluent) and Win10 1809 (MDL2 fallback) with zero app-side font handling.
- Alternatives considered: `Symbol` enum (`Accept`/`Cancel`?) — rejected, enum coverage unverified and `FontIcon`+codepoint is exact; `ChromeClose` E8BB for the cross — rejected, `Cancel` E711 is the standard small × and matches current visual weight.

## Decision 2 — Binding mechanism

- Decision: converters keep `bool → string` contracts and emit codepoint strings (`"\uE73E"`, …); XAML usage sites change from text content to `<FontIcon Glyph="{Binding …}" />` with `FontFamily="{ThemeResource SymbolThemeFontFamily}"` (explicit per skill — never hard-code the family; default fallback would also work but explicit is documented).
- Rationale: `FontIcon.Glyph` is a plain string property, so existing converter shapes survive — smallest possible diff, zero VM/Core changes, behavior preserved by construction.
- Alternatives considered: `FontIconSource` in `App.xaml` + `IconSourceElement` — rejected, overkill for two data-bound sites (sources suit static shared icons); `SymbolIcon` — rejected per Decision 1.

## Decision 3 — Accessibility

- Decision: toggle `Button` gets `AutomationProperties.Name` bound to state ("Active"/"Inactive" via existing label converter or static text + state) + tooltip; lock `FontIcon` wrapped with name "Locked"; empty unlocked cell exposes nothing.
- Rationale: skill rule — screen readers don't announce glyphs; T008 set the precedent (Send/Download/Delete buttons).

## Decision 4 — Tests

- Decision: additive unit asserts on `ActiveGlyphConverter`/`LockedGlyphConverter` outputs (exact codepoint strings + empty-string case); full suites green with zero assertion changes otherwise.
- Rationale: Constitution VI — the converter edit is logic (however thin), so it gets tested first.
