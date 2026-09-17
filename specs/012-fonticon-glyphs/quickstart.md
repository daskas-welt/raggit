# Quickstart: FontIcon Glyphs Validation (012)

## Prerequisites

- .NET 8 SDK (`global.json` pins `8.0.425`); Windows 10 1809+ / 11 for visual checks.

## 1. Converter asserts (fast gate)

```powershell
dotnet test tests/unit/RAGGit.Tests.Unit.csproj --filter "FullyQualifiedName~Glyph"
```

Expect: `ActiveGlyphConverter` true→``, false→``; `LockedGlyphConverter` true→``, false→`""`. (Codepoints: U+E73E CheckMark, U+E711 Cancel, U+E72E Lock.)

## 2. Full regression gate

```powershell
dotnet build RAGGit.sln
dotnet test RAGGit.sln --no-build
dotnet csharpier check .
```

Expect: 0 errors; unit 228+new, contract 78, integration 62 (one known load-dependent perf flake — confirm isolated green); csharpier clean.

## 3. Visual check (Admin, all 3 themes)

1. `dotnet run --project src/RAGGit.Client.WinUI -p:Platform=x64`, sign in as admin → Admin.
2. Light/Dark/Contrast: active rows show check icon, inactive show cross, locked show lock, unlocked cells empty — all legible.
3. Screen reader walkthrough: toggle announces name + state; lock announces "Locked"; empty cells silent.

## 4. Scope guard

```powershell
git status --short -- src/RAGGit.Client.Core tests
```

Expect: Core untouched; tests show only the additive glyph-assert file.
