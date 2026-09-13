# Verification — 004-identity Success Criteria

**Date**: 2026-09-13 | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Quickstart**: [quickstart.md](./quickstart.md)

Each SC is measured by a named procedure; ☐ = not yet run (coder executes during `/speckit.implement` and CI).

## Success Criteria Checklist

| SC | Criterion (tech-agnostic) | Measurement procedure | Pass condition | Status |
|---|---|---|---|---|
| **SC-001** | Provisioned person signs in and reaches the library in < 10 s on LAN | Quickstart step 2 timing: `measure-cmd` around login → `GET /api/auth/me` → first `GET /api/documents` (or client stopwatch) | p95 < 10 s over 10 runs on LAN | ☐ |
| **SC-002** | 100 % of uploads/queries in a signed-in session attributed to the person's stable id | Quickstart step 4 + `IdentityAttributionTests`: N mixed sessions (≥ 2 people, ≥ 20 actions), assert `Documents.CreatedBy`/`Queries.UserId` == token `sub` for every action; zero `"admin"/"employee"` writes from Local sessions | 100 % person-id attribution; 0 shared-key attribution for person actions | ☐ |
| **SC-003** | Deactivated account refused on its very next request | Quickstart step 5.3 + `DeactivationRefusalTests`: hold a valid unexpired JWT, `PATCH isActive=false`, fire next request | next request `401`; zero successful actions after deactivation | ☐ |
| **SC-004** | Credentials and tokens observable only inside HTTPS | Quickstart step 2 wire check + CI: network capture while exercising login/refresh/me/upload over both `http://` and `https://` | no plaintext password/JWT observable; HTTP auth refused or redirected | ☐ |
| **SC-005** | WAN disabled: sign-in and a cited query succeed; no cloud identity provider contacted | Quickstart step 7 + `OfflineIdentityTests` in the WAN-disabled CI leg (Constitution IV harness with socket assertions) | login → me → query with citations succeed; 0 egress beyond LAN/loopback | ☐ |
| **SC-006** | Admin create/role-change/deactivate/reset takes effect without restarting the workstation | Quickstart steps 5.1–5.5 with the API process untouched; assert immediate behavioral effect of each change | every change effective on next request; no process restart | ☐ |

## FR → Artifact Traceability

| FR | Design artifact | Validation |
|---|---|---|
| FR-001 account directory, single-tenant | data-model.md §User (no company_id) | `UserStoreTests`, `UsersCrudContractTests`, I-gate review |
| FR-002 username+password, salted non-recoverable hash | research R2 (PBKDF2 310k) | `PasswordHasherTests` (format, fixed-time verify, no plaintext) |
| FR-003 short-lived signed credential + re-auth | research R1/R4 (JWT 8 h), contracts login/refresh | `AuthLoginContractTests`, US4 client tests |
| FR-004 operator provisioning, no wizard/secret | research R8 (CLI `user add`), quickstart §1 | CLI exit-code tests, boot-with-zero-accounts integration test |
| FR-005 Admin manage via API + Admin screen | contracts `/api/users`, `UsersView` (SfDataGrid) | `UsersCrudContractTests`, `AdminRbacTests`, quickstart §5 |
| FR-006 identity envelope additive/back-compat | contracts `AuthMe` (Local + ApiKey shapes) | `AuthMeAdditiveContractTests` (1.2.0 shape preserved) |
| FR-007 person attribution | data-model.md §Relationships | SC-002 procedure |
| FR-008 HTTPS-only credentials | research R3 | SC-004 procedure |
| FR-009 rate-limit/lockout + expiry | research R5 (5/15 m) + R4 (8 h) | `LockoutPolicyTests`, quickstart §6 |
| FR-010 deactivation kills sessions | data-model.md §State Transitions (OnTokenValidated check) | SC-003 procedure |
| FR-011 fully local resolution, fail-fast offline | research R7 | SC-005 procedure + 503 fail-fast test |
| FR-012 provider configurable; ApiKey kept; AD future | contracts securitySchemes (ApiKey+Bearer), plan §Structure | `RbacContractTests` (existing key paths unbroken) |
| FR-013 additive contract (MINOR) | contracts/api.yaml 1.3.0 diff vs 003's 1.2.0 | contract diff review + 1.2.0 client regression tests |

## Constitution Gates

- **IV Offline Invariant**: SC-005 WAN-disabled identity leg MUST pass in CI before merge (NON-NEGOTIABLE).
- **VI Test-First**: contract tests for 1.3.0 written Red first; ≥ 80 % library coverage (new `RAGGit.Core` identity code); 100 % contract coverage. **Amendment 1.1.0 → 1.2.0 (VI wording, MINOR) recorded in plan.md** — owner approval required before tasks merge.
- **I Single-Tenant**: schema review confirms no tenant column; API review confirms no tenant parameter.
- **VII Simplicity**: no 4th project; one official NuGet (`Microsoft.AspNetCore.Authentication.JwtBearer`); Complexity Tracking empty.

## Exit Criteria for the Feature

All SC rows ☑ · all FR rows verified · WAN-disabled CI leg green · constitution amendment applied to `.specify/memory/constitution.md` with version 1.2.0 · diagrams (`docs/*.html?theme=light`) still match delivered code (node IDs stable) or updated in the same commit.