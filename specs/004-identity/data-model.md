# Data Model — 004-identity (Per-Person Identity & Accounts)

**Date**: 2026-09-13 | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Diagrams**: [docs/dataflow.html](./docs/dataflow.html?theme=light), [docs/architecture.html](./docs/architecture.html?theme=light)

Single-tenant (Constitution I): **no `company_id` / tenant column anywhere**. All timestamps UTC ISO-8601 round-trip (`"O"`) text, matching the existing `RagDbContext` convention (`src/RAGGit.Core/Data/RagDbContext.cs:52`).

## Entities

### User — NEW (per-person account directory)

| Field | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | TEXT (uuid string) | PK, NOT NULL | **Stable person id** — the value written into `Documents.CreatedBy` / `Queries.UserId` (FR-007); never reused, never recycled |
| `Username` | TEXT | NOT NULL, UNIQUE `COLLATE NOCASE`, 3–64 chars, charset `[a-zA-Z0-9._-]` | Case-insensitive match (spec edge case; research R10); display form preserved |
| `DisplayName` | TEXT | NOT NULL, 1–100 chars | Shown in `GET /api/auth/me` envelope (FR-006) and Admin screen |
| `Role` | TEXT | NOT NULL, `CHECK (Role IN ('Admin','Employee'))` | Same two roles as contract 1.2.0 — no new role values (additive) |
| `PasswordHash` | TEXT | NOT NULL | `PBKDF2-SHA256$310000$<b64salt>$<b64hash>` — salted, non-recoverable (FR-002); never serialized by any endpoint |
| `IsActive` | INTEGER (bool) | NOT NULL, DEFAULT 1 | Deactivate ⇒ sign-in refused **and** live tokens refused on next request (FR-010, SC-003) |
| `FailedAccessCount` | INTEGER | NOT NULL, DEFAULT 0 | Consecutive failures; resets on success (R5) |
| `LockoutUntil` | TEXT (nullable) | NULL when not locked | Set to now+15m when `FailedAccessCount` reaches 5 (FR-009) |
| `MustChangePassword` | INTEGER (bool) | NOT NULL, DEFAULT 0 | Admin reset may set 1 ⇒ client prompts change at next sign-in (edge case) |
| `LastSignInAt` | TEXT (nullable) | | Audit only; updated on successful login |
| `LastPasswordChangedAt` | TEXT | NOT NULL | |
| `CreatedAt` | TEXT | NOT NULL | |

Entity source: `src/RAGGit.Core/Models/User.cs` (NEW — library-first per Constitution III; shared by API + operator CLI).

### Session Credential — ephemeral, no table

JWT (HS256, 8 h): `sub` = `User.Id`, `role`, `unique_name` = username, `name` = DisplayName, `iss`/`aud` = `raggit-workstation`, `exp`. Liveness (active + not locked out) is re-checked per request via `OnTokenValidated` against the `Users` row — this is what makes deactivation take effect on the very next request without a revocation list (FR-010 / SC-003 / SC-006, no restart).

### Existing entities — attribution touchpoints (NO column changes)

| Entity | Column | 004 semantics |
|---|---|---|
| `Document` (`Documents` table, `RagDbContext.cs:123–132`) | `CreatedBy TEXT NOT NULL` | Person (Local/JWT) sessions write **`User.Id` (uuid)**; API-key (machine/bootstrap) sessions keep writing `"admin"`/`"employee"`; **legacy rows remain as-is** (spec edge case) — column stays `TEXT`, so no data migration |
| `Query` (`Queries` table, `RagDbContext.cs:144–153`) | `UserId TEXT NOT NULL` | Same rule as above (FR-007, SC-002) |
| `Library` | — | Singleton unchanged (I) |

## Relationships