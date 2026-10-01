# Baseline: Admin People Management Redesign

**Feature**: `026-admin-people-redesign` | **Date**: 2026-10-01 | **Plan**: [plan.md](plan.md)

Pre-change baseline for the redesign; no 026 UI or API changes were present when captured.

## Build and format

| Gate | Result |
|------|--------|
| `dotnet csharpier check .` | ✅ clean (242 files) |
| `dotnet build RAGGit.sln -c Release -p:Platform=x64` | ✅ 0 warnings, 0 errors |
| Unit tests | ✅ 340/340 |
| Contract tests | ✅ 86/86 |
| Offline integration subset | ✅ 12/12 |

## Current Admin page observations

- Title/subtitle and Refresh button at top; status bar below; a four-column Users table; then Create
  Person and Reset Password forms displayed side by side beneath the table.
- Fixed table columns are User / Role / Active / Lock status. There is no search, filter, or sorting.
- Username/display name can ellipsize in the User column; the list is the only place to inspect users.
- Role is a button showing the current role that toggles it on click; Active is a verb button showing
  Activate/Deactivate; the lock column repeats “Not locked” for ordinary accounts.
- Reset form depends on selecting a row and lives below the list; at 800×600, the lower forms require
  scrolling.
- Existing status surface is a polite live region. Existing confirmation flows for role, active state,
  and reset password are present and must be preserved.

## Frozen Admin automation IDs

`RefreshUsersButton`, `AdminStatusBar`, `UsersList`, `NewUsernameBox`, `NewDisplayNameBox`,
`NewRoleCombo`, `NewPasswordBox`, `CreateUserButton`, `ResetPasswordBox`, `ResetMustChangeCheck`,
`ResetPasswordButton`.

## Screenshot references

Captured during feature 025 before this redesign:

- Light Admin page: `%LOCALAPPDATA%\Temp\opencode\walkthrough-025\admin2.png` (1200×800)
- Dark Admin page: `%LOCALAPPDATA%\Temp\opencode\walkthrough-025\admin-dark2.png` (2514×1456)
- Minimum-size Admin page: `%LOCALAPPDATA%\Temp\opencode\walkthrough-025\admin-800x600-dark.png` (816×639)

The temp captures are visual references only; the repository source and frozen IDs remain the baseline
of record.
