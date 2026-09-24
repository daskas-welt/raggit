# Tasks: Per-Person Identity & Accounts

**Input**: Design documents from `specs/004-identity/`

**Prerequisites**: plan.md (required), spec.md, research.md (R1-R10), data-model.md (Users, state transitions), contracts/api.yaml (1.3.0), quickstart.md (7 steps), docs/*.puml (+ rendered .svg in docs/images/, PlantUML)

**Tests**: Constitution VI Test-First NON-NEGOTIABLE — contract/integration tests written first and FAIL before implementation; WAN-disabled suite extended.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1 (operator), US2 (sign-in), US3 (admin), US4 (session)

## Phase 1: Setup

**Purpose**: Tooling and branch prep — no blocking infra

- [ ] T001 Update package references per plan (add `Microsoft.AspNetCore.Authentication.JwtBearer` 8.x to `src/RAGGit.Workstation.Api/RAGGit.Workstation.Api.csproj`) and `dotnet tool restore` verification
- [ ] T002 Configure HTTPS dev certs documentation and `appsettings.json` `Auth:*` skeleton (JwtSigningKeyPath, TokenLifetimeHours=8, Pbkdf2Iterations=310000, LockoutThreshold=5, LockoutMinutes=15)

## Phase 2: Foundational (Blocking Prerequisites)

**CRITICAL**: No user story work until this phase is complete.

- [ ] T003 Amend `.specify/memory/constitution.md` VI `1.1.0 → 1.2.0` to add "local per-person accounts" as configured auth provider (MINOR, with migration note) — owner-approved
- [ ] T004 [P] Create `src/RAGGit.Core/Models/User.cs` entity (Id uuid stable person id, Username unique COLLATE NOCASE, DisplayName, Role Admin|Employee, PasswordHash `PBKDF2-SHA256$310000$`, IsActive, FailedAccessCount, LockoutUntil, MustChangePassword, LastSignInAt, LastPasswordChangedAt, CreatedAt) — per data-model.md
- [ ] T005 [P] Implement `src/RAGGit.Core/Auth/PasswordHasher.cs` — PBKDF2-SHA256 310k, 16-byte salt, self-describing format, `CryptographicOperations.FixedTimeEquals` verify
- [ ] T006 Extend `src/RAGGit.Core/Data/RagDbContext.cs` `GetSchemaCommands()` with `CREATE TABLE IF NOT EXISTS Users (...)` + `IX_Users_Active`, no ALTER on Documents/Queries, no company_id (Constitution I)
- [ ] T007 Add `src/RAGGit.Core/Data/UserStore.cs` (lookup by Username case-insensitive, by Id, create, update) used by API + CLI
- [ ] T008 Configure `src/RAGGit.Workstation.Api/Program.cs` — JwtBearer HS256 with key at `data/auth.key` (256-bit random, creates on first use, NTFS ACL), `OnTokenValidated` DB liveness check (IsActive + not locked), multi-scheme `[Authorize]` Bearer|ApiKey, `UseHttpsRedirection()`
- [ ] T009 Add `src/RAGGit.Workstation.Api/Auth/JwtTokenService.cs` — issue 8h token (sub=User.Id, role, unique_name, name, iss/aud raggit-workstation), validate, ClockSkew 5m

**Checkpoint**: Foundational ready — Users table + hasher + JWT + HTTPS + constitution amendment merged. PlantUML architecture/workflow/sequence/dataflow remain valid as visual contract.

## Phase 3: User Story 1 — Operator provisions the first people (P1)

**Goal**: Operator CLI seeds Admin/Employee with hashed secrets, no wizard, no plaintext in config.

**Independent Test**: Fresh `data/rag.db`, run `raggit user add` for two accounts → sign-in succeeds; verify hashes, no plaintext, no wizard.

### Tests for US1

- [x] T010 [P] [US1] Contract/CLI test for operator verb in `tests/integration/OperatorCliTests.cs` — `user add` creates Users, duplicate case-insensitive → exit 2, invalid role → exit 3
- [x] T011 [P] [US1] Unit test for no-first-run-wizard in `tests/unit/BootWithZeroAccountsTests.cs` — API boots with zero Users and serves `GET /health` / `POST /api/auth/login` → 401

### Implementation for US1

- [x] T012 [US1] Implement `src/RAGGit.Workstation.Api/Cli/OperatorCli.cs` — arg intercept `user add --username --display-name --role Admin|Employee --password-stdin|--password` → UserStore + PasswordHasher, direct DB write, exit codes 0/2/3 (depends on T004-T007)
- [x] T013 [US1] Wire CLI intercept in `src/RAGGit.Workstation.Api/Program.cs` before `builder.Build()` — when `args[0]=="user"` handle and exit, else run host (depends on T012)
- [x] T014 [US1] Document operator CLI in `docs/operator-cli.md` and `specs/004-identity/quickstart.md` step 1 (no secret in config)

**Checkpoint**: US1 independently functional — operator can seed a deployment offline.

## Phase 4: User Story 2 — A person signs in and is identified (P1)

**Goal**: HTTPS login → JWT 8h + SecureStorage, `GET /auth/me` additive envelope, attribution to stable person id.

### Tests for US2

- [x] T015 [P] [US2] Contract test `POST /api/auth/login` in `tests/contract/AuthLoginContractTests.cs` — success 200 `{access_token, token_type, expires_in}`, unknown vs wrong password identical 401, locked → 429 Retry-After, inactive → 401
- [x] T016 [P] [US2] Contract test `GET /api/auth/me` additive in `tests/contract/AuthMeAdditiveContractTests.cs` — Local Bearer → `{identityType:"Local", role, displayName, username, sub}`, ApiKey → 1.2.0 shape, unknown fields ignored
- [x] T017 [P] [US2] Integration test attribution in `tests/integration/IdentityAttributionTests.cs` — bob Employee login → Bearer upload → `Documents.CreatedBy == bob sub`; `POST /api/query` → `Queries.UserId == bob sub`; Employee upload attempt → 403

### Implementation for US2

- [x] T018 [US2] Implement `src/RAGGit.Workstation.Api/Controllers/AuthController.cs` — `POST /api/auth/login` (HTTPS only, PBKDF2 verify, lockout check, 401/429/503, issue JWT) + `GET /api/auth/me` additive envelope (depends on T005, T007-T009)
- [x] T019 [US2] Implement `src/RAGGit.Workstation.Api/Auth/LockoutPolicy.cs` — 5 consecutive fails → 15m LockoutUntil persisted on Users, FixedTimeEquals, reset on success (depends on T007)
- [x] T020 [US2] Touch Documents/Queries attribution path — ensure `CreatedBy`/`UserId` write `User.Id` uuid for Local sessions, legacy literals for ApiKey (no column change) (depends on T006, T018)
- [x] T021 [US2] Add `src/RAGGit.Client.Maui/Services/SessionTokenStore.cs` — `SecureStorage` `raggit.session.token` + `expires_at` (depends on T008)
- [x] T022 [US2] Add `src/RAGGit.Client.Maui/Services/BearerDelegatingHandler.cs` — attach `Authorization: Bearer` when token exists, 401 → clear cache + re-prompt; keep `ApiKeyDelegatingHandler` for bootstrap only, no silent fallback (depends on T021)
- [x] T023 [US2] Update `src/RAGGit.Client.Maui/Services/AuthApiClient.cs` — `LoginAsync` + `RefreshAsync` (optional P3) using HTTPS (depends on T021)

**Checkpoint**: US2 independently functional — sign-in in <10s on LAN, 100% attribution, no enumeration.

## Phase 5: User Story 3 — Admin manages people in-app (P2)

**Goal**: Admin list/create/role/deactivate/reset via API + minimal Admin screen (SfDataGrid).

### Tests for US3

- [x] T024 [P] [US3] Contract test `GET|POST /api/users` in `tests/contract/UsersCrudContractTests.cs` — Admin list/create → 200/201 with PasswordHash never serialized, duplicate → 409, Employee → 403
- [x] T025 [P] [US3] Contract test `PATCH /api/users/{id}` + `POST /reset-password` in `tests/contract/UsersPatchContractTests.cs` — deactivation → next request 401, reset → 204 + MustChangePassword, role change → immediate effect
- [x] T026 [P] [US3] Integration test deactivation refusal in `tests/integration/DeactivationRefusalTests.cs` — hold valid JWT, `PATCH isActive=false` → very next Bearer request 401, 0 further successes
- [x] T027 [P] [US3] Integration test Admin RBAC in `tests/integration/AdminRbacTests.cs` — Employee GET /users → 403

### Implementation for US3

- [x] T028 [US3] Implement `src/RAGGit.Workstation.Api/Controllers/UsersController.cs` — `GET /users` (Admin), `POST /users` (create with PBKDF2, 409 case-insensitive), `GET /users/{id}`, `PATCH /users/{id}` (role/isActive/displayName), `POST /users/{id}/reset-password` (clear lockout, MustChangePassword) — all Admin-only, never serialize PasswordHash (depends on T004-T007)
- [x] T029 [US3] Add `src/RAGGit.Client.Maui/ViewModels/AdminUsersViewModel.cs` — list/create/role/deactivate/reset via `UsersApiClient` (depends on T028)
- [x] T030 [US3] Add `src/RAGGit.Client.Maui/Views/Admin/UsersView.xaml` + `.xaml.cs` — minimal `SfDataGrid` + forms, Admin-only visible (depends on T029)
- [x] T031 [US3] Add `src/RAGGit.Client.Maui/Services/UsersApiClient.cs` — typed CRUD + reset (depends on T028)

**Checkpoint**: US1+US2+US3 independently functional — Admin can manage people without restart (SC-006).

## Phase 6: User Story 4 — Client session lifecycle (P3)

**Goal**: SecureStorage restore, expiry → re-prompt, sign-out clears, refresh optional.

### Tests for US4

- [x] T032 [P] [US4] Unit test `SessionTokenStoreTests.cs` — persist/restore across process kill until expiry, clear on sign-out, never fallback to ApiKey for person actions
- [x] T033 [P] [US4] Integration test session lifecycle in `tests/integration/SessionLifecycleTests.cs` — expired token → 401 → re-login prompt; sign-out → anonymous

### Implementation for US4

- [x] T034 [US4] Add `src/RAGGit.Client.Maui/ViewModels/LoginViewModel.cs` + `Views/LoginView.xaml` — HTTPS login form, actionable cert-trust error, no bypass (depends on T021-T023)
- [x] T035 [US4] Wire `LoginView` navigation + `GET /auth/me` role gating in `src/RAGGit.Client.Maui/App.xaml.cs` / `MauiProgram.cs` — use real person role, not hardcoded (depends on T034, T022)
- [x] T036 [US4] Implement `POST /api/auth/refresh` in `AuthController.cs` + client opportunistic use (optional, P3; trivial re-issue from still-valid token) (depends on T018)

**Checkpoint**: All 4 stories independently functional.

## Phase 7: Polish & Cross-Cutting Concerns

- [x] T037 Update `README.md` Architecture + `specs/001-offline-mode/quickstart.md` + `docs/operator-cli.md` for 1.3.0 (`/auth/login`, `/users`, HTTPS, Operator CLI)
- [x] T038 Run `specs/004-identity/quickstart.md` steps 1-7 validation (operator CLI → HTTPS login → me → upload attribution → admin create/deactivate → lockout → offline proof) and fill `specs/004-identity/verification.md` SC-001..006
- [x] T039 Extend WAN-disabled CI leg with identity steps (provision → login → me → cited query, 0 egress beyond LAN/loopback, SC-005) in `.github/workflows/ci.yml`
- [x] T040 Security hardening — ensure no hard-coded URLs/keys per `ClientConfigTests`, HTTPS-only enforcement for auth, PBKDF2 fixed-time verify, no PasswordHash serialization
- [x] T041 Maintain PlantUML diagrams — if any topology/flow/state drifted during implementation, update affected `specs/004-identity/docs/*.puml` and re-render same commit (was legacy `*.json` before removal 2026-09-24)
- [x] T042 Cleanup — `dotnet csharpier check .`, `dotnet build`, `dotnet test` (contract 100%, library ≥80%), remove any placeholder `.write-probe.txt`

## Dependencies & Execution Order

- **Phase 1 → Phase 2** strict; Phase 2 blocks all stories
- **US1 → US2 → US3 → US4** in priority order; each independently testable after Foundational
- **Phase 7** depends on all desired stories
- Parallel: T004+T005, T015-T017, T024-T027, T032-T033, T041 can run in parallel within phase (different files)
- Tests MUST FAIL before implementation per Constitution VI

## Traceability

- FR-001 → T004, T006, T012, T028
- FR-002 → T005, T015, T018
- FR-003 → T008-T009, T018, T021-T023, T036
- FR-004 → T012-T014
- FR-005 → T028-T031
- FR-006 → T016, T018
- FR-007 → T017, T020
- FR-008 → T008, T018, T023-T024, T040
- FR-009 → T019, T015, T038 step 6
- FR-010 → T008 (OnTokenValidated), T026
- FR-011 → T039, T033
- FR-012 → T008, T018 (ApiKey retained)
- FR-013 → T016, contracts 1.3.0 additive
