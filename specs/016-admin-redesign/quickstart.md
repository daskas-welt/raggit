# Quickstart: Admin Page Redesign

**Feature**: `016-admin-redesign` | **Date**: 2026-09-18 | **Plan**: [plan.md](plan.md) | **Contract**: [contracts/admin-page.md](contracts/admin-page.md)

Validation guide proving the feature works end-to-end. No implementation code here — see the contract and data model for details.

## Prerequisites

- .NET 10 SDK, Windows 10 1809+ / 11 (WinUI 3, Windows App SDK 2.4.0)
- Workstation API running on LAN with seeded accounts; an admin session
- Repo root: `C:\Users\mcaib\Documents\Projects\raggit`
- Note: close Visual Studio (or expect PDB-copy warnings) and stop any running `RAGGit.Workstation.Api` before rebuilding test projects — both hold file locks

## 1. Build

```powershell
dotnet build src\RAGGit.Client.WinUI\RAGGit.Client.WinUI.csproj -p:Platform=x64
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

## 2. Unit tests (admin status behavior)

```powershell
dotnet test tests\unit\RAGGit.Tests.Unit.csproj --filter "FullyQualifiedName~AdminUsersStatusTests"
```

Expected: `Passed! - Failed: 0, Passed: 7` (status/severity state, per-command confirmations, field-naming validation failures, value preservation). The 3 pre-existing admin tests pass with zero assertion changes.

## 3. Manual walkthrough (admin, Admin page)

1. Open Admin → three distinct sections in a readable column: user list, Create Person, Reset Password.
2. Change a role → confirm dialog → success `InfoBar`, list reflects the new role.
3. Toggle active → confirm → success `InfoBar`, row updates.
4. Create with a duplicate username → prominent message naming the conflict; entered values preserved.
5. Reset with no user selected → message explains a selection is required.
6. Reset with a user selected → confirm → success `InfoBar`.
7. Refresh → loading indicator visible; buttons gated while busy.

## 4. Accessibility + themes (manual)

- Keyboard only: Tab order header → rows → row actions → forms → confirms; Enter activates; Esc dismisses confirms; visible focus throughout.
- Screen reader (Narrator): row actions announce which user they affect; `InfoBar` outcomes announced.
- Light / Dark / HighContrast: rows, forms, InfoBar, and focus indicators legible; narrow window (≤720px) stacks forms with nothing clipped.
- Non-admin session: Admin nav item absent; page unreachable.

## References

- Presentation state and rules: [data-model.md](data-model.md)
- Binding + behavior rules: [contracts/admin-page.md](contracts/admin-page.md)
- Decisions and rejected alternatives: [research.md](research.md)
