# Implementation Plan: Admin People Management Redesign

**Branch**: `026-admin-people-redesign` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/026-admin-people-redesign/spec.md`

## Summary

Redesign the Admin People Management screen as a master-detail workspace: searchable, filterable,
sortable people list on the left and selected-account details on the right, stacked below the list at
narrow content widths. Move create and reset forms into focused modal dialogs, represent account state
with explicit chips and actions, and preserve the existing frozen automation IDs.

Add one necessary server-side safety rule: an admin PATCH cannot demote or deactivate the final active
Admin. This must be enforced atomically with the persisted update, not by a client-side count or a
process-local lock. Refusal returns HTTP 409 with a user-safe message; the client surfaces it without
changing the displayed account.

Search, role/status filters, sorting, and selection stay in the presentation layer and are not persisted
or sent as server-side query parameters. No new user fields, table, or project is introduced.

## Technical Context

**Language/Version**: C# / XAML, .NET 10 (`net10.0-windows10.0.17763.0` client; `net10.0` Core/API)

**Primary Dependencies**: WPF-UI `4.3.0`, CommunityToolkit.Mvvm, ASP.NET Core, Microsoft.Data.Sqlite — all already referenced; no new package.

**Storage**: Existing SQLite `Users` table only. No schema changes or new persisted fields. The guarded PATCH runs within a serialized SQLite write transaction.

**Testing**: TDD required. Add/extend unit tests for role command/filter coordination and repository invariant; contract + integration tests for refusal and allowed updates; then run `dotnet build RAGGit.sln -c Release -p:Platform=x64`, unit, contract, offline integration suites, `dotnet csharpier check .`, and the UI walkthrough in [quickstart.md](quickstart.md).

**Target Platform**: Windows 10 1809+ / Windows 11 desktop; workstation API remains local/LAN-only.

**Project Type**: WPF desktop client + existing ASP.NET Core workstation API and shared .NET libraries.

**Performance Goals**: A 100-account directory remains smoothly scrollable and local search/filter/sort completes without noticeable delay; no extra list request is made for local view operations.

**Constraints**: Preserve the 11 frozen Admin automation IDs; no new persistent state; view-only search/filter/sort; preserve validation, RBAC, existing API operations, theme tokens, keyboard/screen-reader support, ≥44-DIP interactive targets, and 800×600 usability. The sole API behavior addition is the last-active-admin PATCH guard and documented 409 response.

**Scale/Scope**: Admin page, two modal form dialogs, the shared AdminUsersViewModel command surface, `IUserRepository` and its SQLite implementation, UsersController, the user PATCH OpenAPI contract, and focused tests. No new project or package.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Single-Tenant On-Prem**: PASS — no tenant or data partitioning changes.
- **II. Workstation-Owned AI**: PASS — no client AI/model/vector/network destination change; the client remains HTTP-only to the configured workstation.
- **III. .NET Library-First & Client Reuse**: PASS — uses existing projects and service layers; no new project and no duplicated RAG logic.
- **IV. Offline Invariant**: PASS — the guard uses the local Users store; no WAN path is added.
- **V. Citation-Grounded RAG**: PASS — query, retrieval, answer, and citation behavior are untouched.
- **VI. Test-First (NON-NEGOTIABLE)**: PASS — repository/API guard tests are written and made to fail before implementation; contract and integration coverage prove the 409 and the allowed path. Existing offline tests remain required.
- **VII. Simplicity & Proprietary Stewardship**: PASS — no project/package/artifact format changes; filters are transient and no data schema is added.

**Pre-Phase-0 gate**: PASS. No constitutional violation requires an exception.

## Project Structure

### Documentation (this feature)

```text
specs/026-admin-people-redesign/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── admin-page.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
src/RAGGit.Client.WPF/
├── Views/Pages/AdminUsersPage.xaml(.cs)         # master-detail list, local view, responsive layout
└── Views/Dialogs/
    ├── CreatePersonDialog.xaml(.cs)             # create form
    └── ResetPasswordDialog.xaml(.cs)             # targeted reset form

src/RAGGit.Client.Core/
└── ViewModels/AdminUsersViewModel.cs            # explicit role update command; existing account actions

src/RAGGit.Core/
├── Abstractions/Repositories/IUserRepository.cs # atomic guarded-patch result seam
├── Models/UserPatchResult.cs                    # transient update outcome, no persistence
└── Data/SqliteUserRepository.cs                  # serialized guard + update transaction

src/RAGGit.Workstation.Api/
├── Controllers/UsersController.cs                # map guarded refusal to 409 Error response
└── RAGGit.Workstation.Api.csproj                 # API version alignment to 1.4.0

specs/004-identity/contracts/api.yaml              # document PATCH 409; additive API contract v1.4.0

tests/
├── unit/AdminUsersViewModelTests.cs               # explicit role choice and existing UI state
├── unit/UsersApiClientTests.cs                    # user-safe conflict parsing
├── contract/UsersPatchContractTests.cs            # 409 response
└── integration/LastActiveAdminGuardTests.cs       # refusal and concurrent updates
```

**Structure Decision**: Keep the existing solution/project boundaries. WPF view filtering/sorting belongs to `AdminUsersPage`'s `ICollectionView`; action state and commands remain in the shared client view model. The last-admin invariant belongs at the persistence boundary so concurrent API requests cannot both pass a stale count check. Create/reset remain modal windows within the existing WPF client, following its existing dialog ownership pattern.

## Complexity Tracking

No constitutional violations or new projects/packages require an exception.

| Added seam | Why needed | Simpler alternative rejected because |
|------------|------------|---------------------------------------|
| Transactional repository patch operation | The final-admin invariant must remain true under overlapping PATCH requests and across API instances using the same SQLite store. | Controller-side or UI-side count-then-update can race and lock out every admin. |
| Two modal form windows | Focus create/reset flows without keeping large forms permanently on the Admin page; retain existing bindings/validation and stable IDs. | Inline forms preserve the clutter the redesign explicitly removes; a hand-built content dialog would duplicate form/layout code. |

**Post-Phase-1 gate**: PASS — no new project, dependency, schema, or WAN behavior; the API contract change is bounded to the documented 409 on PATCH.
