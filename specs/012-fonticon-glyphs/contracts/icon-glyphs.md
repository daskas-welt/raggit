# UI Contract: Status Glyph Icons (012)

Binding contract between `AdminUsersPage.xaml` and the glyph converters. XAML binds; converters format; ViewModels are untouched.

## Active toggle (Button content)

```xml
<Button AutomationProperties.Name="Toggle user active state"
        ToolTipService.ToolTip="Toggle user active state"
        Command="{Binding DataContext.ToggleActiveCommand, ElementName=UsersList}"
        CommandParameter="{Binding}">
    <FontIcon Glyph="{Binding IsActive, Converter={StaticResource ActiveGlyph}}"
              FontFamily="{ThemeResource SymbolThemeFontFamily}" />
</Button>
```

- `ActiveGlyphConverter.Convert(true)` → `"\uE73E"` · `Convert(false)` → `"\uE711"`.
- Foreground stays `Primary` (existing).

## Locked indicator

```xml
<FontIcon Glyph="{Binding LockedOut, Converter={StaticResource LockedGlyph}}"
          FontFamily="{ThemeResource SymbolThemeFontFamily}"
          AutomationProperties.Name="Locked" />
```

- `LockedGlyphConverter.Convert(true)` → `"\uE72E"` · `Convert(false)` → `""` (empty cell).
- Unlocked rows render nothing and expose no accessible name (empty string has no automation footprint; do NOT set a static Name on the shared template element — set it conditionally or accept Narrator silence on empty cells).

## Invariants

- Converter signatures unchanged (`IValueConverter`, bool→string); `ConvertBack` keeps throwing `NotSupportedException`.
- No new resource keys required (converters already keyed in page resources).
- Codepoints render via `SymbolThemeFontFamily` on Win11 (Fluent) and Win10 1809 (MDL2 fallback).
