# Quickstart: Admin People Management Redesign

**Feature**: `026-admin-people-redesign` | **Date**: 2026-10-01 | **Plan**: [plan.md](plan.md)

Validation guide for [contracts/admin-page.md](contracts/admin-page.md), the feature outcomes in
[spec.md](spec.md), and the existing user-management contract.

## Prerequisites

- Windows 10 1809+ / Windows 11; .NET 10 SDK pinned by `global.json`.
- `winapp` CLI for UI Automation and screenshot walkthrough.
- Local workstation API credentials and an Admin account for interactive surfaces.

## Build, format, and regression gates

```powershell
dotnet tool restore
dotnet csharpier check .
dotnet build RAGGit.sln -c Release -p:Platform=x64
dotnet test tests/unit/RAGGit.Tests.Unit.csproj -c Release --filter "FullyQualifiedName~Tests.Unit"
dotnet test tests/contract/RAGGit.Tests.Contract.csproj -c Release --filter "FullyQualifiedName~Tests.Contract"
dotnet test tests/integration/RAGGit.Tests.Integration.csproj -c Release --filter "FullyQualifiedName~Tests.Integration"
```

The offline integration subset remains independently runnable:

```powershell
dotnet test tests/integration/RAGGit.Tests.Integration.csproj -c Release --filter "QueryOfflineTests|OfflineIdentityTests|OfflineHistoryTests|CrossUserIsolationTests"
```

## Contract and invariant tests

Before implementing the repository guard, add failing tests to
`tests/contract/UsersPatchContractTests.cs` and/or `tests/integration/LastActiveAdminGuardTests.cs`:

1. One active Admin: PATCH demotion returns 409 Error; stored Role remains Admin.
2. One active Admin: PATCH `isActive=false` returns 409 Error; stored IsActive remains true.
3. Two active Admins: demote/deactivate one; PATCH returns 200 and the other remains active Admin.
4. Non-admin target or display-name-only update: existing success behavior remains.
5. Concurrent attempts against two active admins: at most one removal succeeds and one active Admin
   remains.

Use the existing `TestApiFactory`/`IntegrationTestFactory` fixtures and their Admin API key or issued
tokens; do not create tests against the developer database.

## Interactive UI walkthrough

Run from the repo root after starting the workstation API and signing in as Admin. Capture each page at
the minimum window size and a wide desktop size; force a repaint after navigation before screenshots
(see the `023` guide's screenshot note).

1. **Directory** — verify search matches username and display name; Role and Active filters combine;
   clearing filters restores all rows; sortable headers indicate direction; detail follows selection.
2. **Selection** — filter out the selected row; details clear and row actions cannot target it.
3. **Role** — choose the other role; cancel and verify no change; choose again, confirm, and verify the
   row/detail update. Repeat on your own account and verify the access warning.
4. **Active/lock** — Active/Disabled chip matches state; Activate/Deactivate is a distinct action;
   Locked appears only when locked; no unlock action appears.
5. **Create** — open Add person; verify fields and cancel; submit missing/duplicate data and verify the
   form retains values; create a valid account and verify it appears.
6. **Reset** — open reset for a row, verify target identity, cancel; repeat from detail; verify the
   existing confirmation, must-change checkbox, success, and failure preservation.
7. **Last Admin** — with one active Admin, attempt demotion and deactivation: both return the 409
   explanation and preserve state. With two active Admins, removing one is allowed.
8. **Accessibility/theme** — keyboard through filters/list/details/dialogs; screen reader announces row
   identity and action; cycle Light/Dark/High Contrast; confirm all actions remain reachable at 800×600.

## Expected outcomes

| Check | Contract | Pass condition |
|-------|----------|----------------|
| Local directory view | U1 | search/filter/sort operate without API calls; selected details match visible row |
| Responsive layout | U1/U4 | panel side-by-side when wide; stacked at ≤720 DIPs; no clipped controls at 800×600 |
| Role/status safety | U2 | role cancel restores; explicit action and chip communicate distinct things |
| Dialog outcomes | U3 | target is explicit; failures retain form; success refreshes and closes |
| Last-admin invariant | A1 | one active Admin cannot be removed; concurrent attempts leave at least one |
| Automation/accessibility | U3/U4 | frozen 11 Admin IDs remain; new controls have stable IDs/names |
| Regression gates | U4 | CSharpier, full build, unit/contract/integration and offline subset pass |
