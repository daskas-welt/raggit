# Data Model: FontIcon Glyphs (012)

No persistent data. The "model" is the converter output contract:

## Glyph mapping (bool → codepoint string)

| Converter | Input `true` | Input `false` | Null/other |
|---|---|---|---|
| `ActiveGlyphConverter` | `"\uE73E"` (CheckMark) | `"\uE711"` (Cancel) | `"\uE711"` (falsy default, preserves current `value is true` semantics) |
| `LockedGlyphConverter` | `"\uE72E"` (Lock) | `""` (empty → empty cell) | `""` |

## Validation rules

- Outputs MUST be exactly these strings (unit-asserted); no other codepoints.
- `FontIcon.Glyph` binding consumes the string directly; empty string renders nothing (no placeholder).
- `Foreground` always theme-driven (`Primary` for toggle, default text for lock) — never hard-coded, per 011 theme system.
- State transitions: none (pure function of bound bool; no animation).
