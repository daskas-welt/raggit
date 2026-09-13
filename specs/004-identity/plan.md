# Implementation Plan: Per-Person Identity & Accounts

**Branch**: `004-identity` | **Date**: 2026-09-13 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/004-identity/spec.md`

## Summary

Replace shared-key identity for people with local per-person accounts on the AI Workstation. The operator provisions the first accounts with a local CLI verb (no first-run wizard, no plaintext secret in config); the workstation issues short-lived (8h) JWT bearer tokens via `Microsoft.AspNetCore.Authentication.JwtBearer` (HS256, workstation-held key), verifying passwords against a PBKDF2-SHA256 (310k iterations) hash in a new SQLite `Users` table; Admins manage people through additive `/api/users` endpoints and a minimal `SfDataGrid` screen in the existing MAUI client; all credential/token traffic is HTTPS-only. Uploads and queries are attributed to the stable person id (`User.Id` uuid) in `Documents.CreatedBy` / `Queries.UserId`; legacy `"admin"/"employee"` rows remain as-is. Contract bumps `1.2.0 → 1.3.0` (additive/MINOR, FR-013); Constitution `VI` receives a MINOR amendment (`1.1.0 → 1.2.0`) to enumerate local per-person accounts as a configured auth provider — recorded below; `constitution.md` itself is edited in a separate owner-approved commit.

## Technical Context

**Language/Version**: C# / .NET 8 (server + libraries `net8.0`; client `net8.0-windows10.0.19041.0` first — mobile deferred per `002` Q1)

**Primary Dependencies**: Existing ASP.NET Core stack + **one new package**: `Microsoft.AspNetCore.Authentication.JwtBearer` (8.x, official); `Microsoft.Data.Sqlite` (existing, via `RagDbContext`); `Rfc2898DeriveBytes.Pbkdf2` (BCL — no new crypto package); MAUI `SecureStorage` (built-in Essentials); Syncfusion `SfDataGrid` (existing licensed stack)

**Storage**: SQLite `data/rag.db` — new `Users` table (additive DDL in `RagDbContext.GetSchemaCommands()`, `src/RAGGit.Core/Data/RagDbContext.cs:113`); JWT signing key at `data/auth.key` (256-bit random, created on first use, NTFS ACL to service account)

**Testing**: xUnit — unit (`tests/unit/`), contract (`tests/contract/`, OpenAPI-driven, 100% contract coverage per Constitution VI), integration (`tests/integration/`, includes the WAN-disabled offline suite which 004 extends)

**Target Platform**: AI Workstation (customer-owned, LAN) + Windows 11 thin client (MAUI)

**Project Type**: Web-service + desktop client (existing 4-project layout; **no new projects** — Constitution VII)

**Performance Goals**: Sign-in → attributed library access < 10 s on LAN (SC-001); PBKDF2 310k ≈ 250–500 ms per verification, login path only (never per request); per-request auth = JWT validate + one indexed `Users` lookup

**Constraints**: Offline Invariant NON-NEGOTIABLE (IV) — all auth resolves on-workstation, zero cloud IdP egress, fail-fast 503 on store failure; HTTPS-only credentials/tokens (FR-008); single-tenant, no `company_id` (I); lockout 5 attempts / 15 min; token 8 h; additive contract only (FR-013)

**Scale/Scope**: 1 workstation, ~10–500 person accounts, 2 roles (Admin/Employee); 4 user stories (P1–P3), FR-001..013, SC-001..006; identity + attribution only — per-person query history is a later feature (spec Q6)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Pre-Phase 0 (2026-09-13)

| Principle | Status | Notes |
|---|---|---|
| I. Single-Tenant On-Prem | ✅ PASS | Account directory is per-deployment; no tenant/company dimension in `Users` (data-model.md); no tenant parameter in API |
| II. Workstation-Owned AI | ✅ PASS | All identity logic on workstation; client stays thin — token cache + Bearer header only, no local identity logic |
| III. .NET Library-First | ✅ PASS | `User` entity + `PasswordHasher` live in `RAGGit.Core` (independently testable); API and operator CLI consume the same library |
| IV. Offline Invariant | ✅ PASS | Sign-in/me/users resolve locally; WAN-disabled CI extended with login → me → cited query (research R7); store failure = fail-fast 503, no hang, no remote fallback (FR-011) |
| VI. Test-First | ⚠️ **AMENDMENT REQUIRED (MINOR)** | Gate wording lists "configured auth provider (API key or Windows AD)"; 004 adds local per-person accounts → **Constitution 1.1.0 → 1.2.0**, recorded below. TDD: contract tests for 1.3.0 endpoints written first (Red-Green-Refactor) |
| VII. Simplicity & Stewardship | ✅ PASS | No 4th project: operator CLI = argument intercept inside existing `RAGGit.Workstation.Api` assembly; Admin screen = new view in existing `RAGGit.Client.Maui`; one official NuGet (JwtBearer); versioning MINOR additive `1.2.0 → 1.3.0` |

**Gate result**: PASS conditional on recording the VI amendment (below). No Complexity Tracking entries.

### Amendment Record — Constitution VI (to apply to `.specify/memory/constitution.md` in a separate owner-approved commit)

- **Version change**: 1.1.0 → 1.2.0 (MINOR — expanded guidance; no principle removed or redefined)
- **Modified principle**: VI. Test-First — integration gate wording "configured auth provider (API key or Windows AD)" → "configured auth provider (API key, **local per-person accounts**, or Windows AD)"
- **Rationale**: 004 resolves spec Clarifications Q1–Q3: local per-person accounts are the per-person provider; API key remains valid for machine/bootstrap; Windows AD remains a documented future provider (FR-012)
- **Migration plan**: this `plan.md` records the amendment for `004-identity`; specs 001–003 are historical and unaffected; `tasks.md` for 004 cites the amended wording; owner approval per Governance §Amendments
- **Status**: pending owner approval at time of writing — blocks `/speckit.tasks` until applied

### Post-Phase 1 (2026-09-13, after data-model + contracts + diagrams)

All rows above remain **PASS**. The design introduced no new project, no cloud dependency, no tenant dimension, and no breaking contract change: `AuthMe` gains optional additive fields only; `Document.createdBy` / `Query.userId` remain strings (legacy literals valid). FR-010 (deactivation kills live sessions) is met by an `OnTokenValidated` DB check — one indexed lookup per request, no revocation-list infrastructure (VII). Amendment gate unchanged. **Gate: PASS** (with the VI MINOR amendment recorded).

## Phase 0 — Research Tasks

Resolved in [research.md](./research.md):

| ID | Question | Decision |
|---|---|---|
| R1 | JWT bearer official vs hand-rolled HMAC | `Microsoft.AspNetCore.Authentication.JwtBearer`, HS256, workstation key at `data/auth.key` |
| R2 | PBKDF2 parameters | SHA-256, **310,000 iterations**, 16-byte salt, self-describing hash format, fixed-time verify |
| R3 | HTTPS self-signed guidance for LAN | dev-certs (dev) / internal CA or distributed self-signed (prod); **no bypass code**; actionable trust error |
| R4 | Token lifetime | 8 h configurable; 5-min framework clock-skew = documented bounded behavior |
| R5 | Lockout policy | 5 consecutive failures → 15 min account-scoped lockout, DB-persisted, 429 |
| R6 | SecureStorage cache + refresh | `SecureStorage.Default` + `BearerDelegatingHandler`; 401 → re-prompt; `/auth/refresh` implemented but optional (P3) |
| R7 | Offline-invariant proof | WAN-disabled CI: provision → login → me → cited query; fail-fast 503 |
| R8 | Operator CLI verb | `raggit user add` (arg intercept in `Workstation.Api`, no new project, direct DB write) |
| R9 | Diagram theme | viewer-runtime; reference `?theme=light`; light visual-check evidence committed |
| R10 | Username case handling | `COLLATE NOCASE` unique index; case-insensitive lookup |

## Phase 1 — Design Artifacts

- **Data model**: [data-model.md](./data-model.md) — `User` entity (Id uuid = stable person id, case-insensitive unique Username, DisplayName, Role, PBKDF2 PasswordHash, IsActive, FailedAccessCount, LockoutUntil, MustChangePassword, LastSignInAt, LastPasswordChangedAt, CreatedAt), account state transitions (Active / LockedOut / Inactive), attribution relationships, additive migration SQL, **no `company_id`** (I)
- **API contract**: [contracts/api.yaml](./contracts/api.yaml) — OpenAPI **1.3.0** additive over 003's 1.2.0: `POST /api/auth/login`, `POST /api/auth/refresh`, `GET /api/auth/me` additive envelope (`displayName`/`username`/`sub`), `GET|POST /api/users`, `PATCH /api/users/{id}`, `POST /api/users/{id}/reset-password` (Admin-only), `Bearer` scheme alongside retained `ApiKey`, 401/403/429, HTTPS server
- **Quickstart**: [quickstart.md](./quickstart.md) — 7 validation steps (operator CLI provision → HTTPS login → me → upload attribution → admin create/deactivate → lockout → offline proof)
- **Verification**: [verification.md](./verification.md) — SC-001..006 measurement checklist + FR traceability
- **Tasks**: `tasks.md` is Phase 2 output of `/speckit.tasks` — NOT created by this plan

## Architecture Diagrams (Archify — visual contract for /speckit.tasks + coder)

Delivered showcase-quality, light-theme-legible standalone HTML under `specs/004-identity/docs/`. Theme is viewer-runtime (not a JSON field): open with `?theme=light`; committed `*.visual-check.*.light.png` (1440×900 and 2048×1320) are the light-theme evidence. **Node IDs are stable** — code artifacts map to them.

| Diagram | File (append `?theme=light`) | Covers |
|---|---|---|
| Architecture | [docs/architecture.html](./docs/architecture.html?theme=light) | Topology & boundaries: `maui-client`, `operator-cli`, `workstation-api`, `jwt-auth`, `users-db`, `lancedb`, `ollama`, `eval-tests`; LAN region + `sg-workstation` (:5001 HTTPS, :11434 loopback) |
| Workflow | [docs/workflow.html](./docs/workflow.html?theme=light) | Runbook provision → authenticate → use & manage: `cli-provision`, `seed-users`, `login`, `jwt-issue`, `me`, `upload-query`, `stored`, `admin-manage`, `deactivate-lockout`, `retry-login` |
| Sequence | [docs/sequence.html](./docs/sequence.html?theme=light) | Request lifecycle: login (PBKDF2 verify → issue 8h), `me` (Bearer validate → role+displayName), documents (CreatedBy = person id), users (Admin), expiry → 401 → re-login |
| Data-flow | [docs/dataflow.html](./docs/dataflow.html?theme=light) | Credential & attribution lineage: operator → `users` → `jwt` → `client-cache` (SecureStorage) → `docs-table`/`queries-table` → `admin-screen`/`audit` |
| Lifecycle | **deferred (conditional)** | Account states (Active ⇄ LockedOut, Active → Inactive → Active) are fully specified in data-model.md §State Transitions and visualized via the workflow `deactivate-lockout` node; a standalone lifecycle diagram is deferred until per-person query history (later feature) or richer account states justify it |

## Project Structure

### Documentation (this feature)