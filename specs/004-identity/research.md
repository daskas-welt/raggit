# Phase 0 Research — 004-identity (Per-Person Identity & Accounts)

**Date**: 2026-09-13 | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

Resolves every open design question behind spec Clarifications Q1–Q6. No `[NEEDS CLARIFICATION]` remains; each decision lists rejected alternatives per Constitution VII (simplicity) and IV (offline).

## R1 — Session credential: official JWT bearer vs hand-rolled HMAC

**Decision**: `Microsoft.AspNetCore.Authentication.JwtBearer` (official .NET 8 package) with **HS256** symmetric signing by a workstation-held key. Default scheme stays `ApiKey` (machine/bootstrap, FR-012); human endpoints accept `Bearer` or `ApiKey` via explicit multi-scheme `[Authorize]`.

**Details**:
- Signing key: 256-bit random at `data/auth.key`, created on first use, NTFS ACL restricted to the service account, survives restarts (sessions not invalidated by maintenance restarts). Override path via `Auth:JwtSigningKeyPath`.
- Claims: `sub` = `User.Id` (uuid — the stable person id used for attribution), `role`, `unique_name` = username, `name` = display name; `iss` = `aud` = `raggit-workstation`.
- FR-010 (deactivation must kill live sessions): `OnTokenValidated` loads the user by `sub` and fails if `!IsActive` or currently locked out → next request 401 (SC-003) with no revocation list.

**Why official, not hand-rolled HMAC**: JwtBearer implements signature + `exp`/`nbf`/`iss`/`aud` + skew validation to a hardened standard; a hand-rolled `HMACSHA256(token)` comparison re-creates that attack surface (alg-confusion, timing, expiry edge cases) for zero benefit on a single node (VII).
**HS256 vs RS256**: the issuer is the only verifier — no third party needs a public key; RS256 adds key-pair ceremony without security gain here.
**Rejected**: cookie/session state (bearer is the right shape for a cross-origin MAUI HttpClient); ASP.NET Identity / full auth frameworks (huge dependency for 2 roles on an offline box — violates VII); refresh-token rotation ceremony (see R6).

## R2 — PBKDF2 parameters

**Decision**: PBKDF2-HMAC-**SHA256**, **310,000 iterations**, 16-byte random salt, 32-byte derived key, using BCL `Rfc2898DeriveBytes.Pbkdf2` (no new NuGet); verification via `CryptographicOperations.FixedTimeEquals`.

**Stored format (self-describing)**: `PBKDF2-SHA256$310000$<base64salt>$<base64hash>` — the iteration count is embedded so a future cost bump is a MINOR change that re-hashes on next successful login without breaking existing rows.

**Rationale**: 310k SHA-256 iterations ≈ 250–500 ms on workstation-class hardware — deliberate friction on the **login-only** path (never per request); comfortably above ASP.NET Identity's 100k v3 default and aligned with current OWASP Password Storage guidance territory for PBKDF2-SHA256; configurable via `Auth:Pbkdf2Iterations`. FR-002's "salted, non-recoverable" is satisfied; plaintext never leaves the hasher.
**Rejected**: Argon2id/bcrypt (native dependency for a LAN appliance; PBKDF2 is BCL, FIPS-adjacent, and sufficient at this threat model); raw SHA-256 (no salt/iterations — violates FR-002).

## R3 — HTTPS on the LAN (self-signed guidance)

**Decision**: Kestrel serves :5001 over HTTPS; `app.UseHttpsRedirection()`; credential/token traffic is HTTPS-only (FR-008). The HTTP :5000 listener remains **Development-environment only** for the existing in-memory test harness (`tests/contract/TestApiFactory.cs`).

**Certificate guidance (operational, not code)**:
- Dev: `dotnet dev-certs https --trust` on workstation + client.
- Production LAN: internal CA-issued cert preferred; otherwise a self-signed cert whose public side is distributed by the operator and installed into the client machines' Trusted Root store (operator has OS access — same trust anchor as provisioning).
- **No bypass in code**: the client never disables certificate validation. An untrusted cert yields a clear, actionable error ("workstation certificate not trusted — see quickstart step 2"), never a silent accept (spec edge case).
- SC-004 wire proof: capture shows password + JWT only inside TLS; plaintext HTTP auth requests are redirected/refused.

**Rejected**: mTLS per-client certs (distribution burden exceeds value); "HTTPS off on a trusted VLAN" (violates FR-008).

## R4 — Token lifetime & clock skew

**Decision**: 8 hours (`Auth:TokenLifetimeHours`, default 8). `TokenValidationParameters.ClockSkew` stays at the framework default 5 minutes → **bounded behavior documented**: with worst-case skew a token lives ≤ 8 h 05 m; the workstation clock is the single time authority (validation makes no network calls — IV-safe).

**Rationale**: one sign-in covers a full workday (SC-001 UX), expiry re-prompts (US4), refresh (R6) lets a long session renew without retyping while still bounding absolute age.
**Rejected**: 15-min access tokens + rotation (OAuth ceremony unjustified for a 2-role LAN app — VII); non-expiring tokens (violates FR-003).

## R5 — Lockout / rate limiting

**Decision**: **5 consecutive failed sign-ins → 15-minute lockout** (`Auth:LockoutThreshold` = 5, `Auth:LockoutMinutes` = 15), persisted on the `Users` row (`FailedAccessCount`, `LockoutUntil`) so it survives process restarts and is **account-scoped** (the meaningful unit on a LAN with few source addresses). Counter resets on success or when `LockoutUntil` passes. Locked sign-in attempts return **429**; wrong password and unknown username return the **identical generic 401** (no enumeration, US2 scenario 3).

**Rejected**: IP-based sliding window (shared/rotating LAN addresses; account-scoped is the correct policy surface); in-memory counters (restart resets the attacker's budget); global rate limit only (doesn't stop password-spraying one account).

## R6 — Client session cache (SecureStorage) & refresh

**Decision**: MAUI `SecureStorage.Default` keys `raggit.session.token` / `raggit.session.expires_at`; new `BearerDelegatingHandler` attaches `Authorization: Bearer` when a token exists and is unexpired; any 401 → clear cache → re-prompt sign-in. `ApiKeyDelegatingHandler` (existing) stays for machine/bootstrap config only — **no silent fallback to a shared key for person actions** (US4 scenario 2, FR-007). `POST /api/auth/refresh` is specified and implemented (P3 priority, trivial re-issue from a still-valid token) but the client uses it only opportunistically before expiry.

**Rationale**: SecureStorage maps to DPAPI/Keychain/Keystore per platform — the token cache is the only client-side state (Assumption: client has no identity logic beyond cache). Process-kill → relaunch restores the session until expiry (US4 scenario 1).
**Rejected**: plaintext file cache (token theft); refresh-token rotation (ceremony without benefit at 8 h/LAN scale).

## R7 — Offline-invariant proof (Constitution IV extension)

**Decision**: the WAN-disabled CI suite gains an identity leg: operator CLI provision → `POST /api/auth/login` → `GET /api/auth/me` → `POST /api/query` (Bearer) → cited answer — all with zero outbound sockets beyond LAN/loopback (existing harness pattern, e.g. `tests/integration/RealOfflineFailFastTests.cs`). If the identity store is unreachable the API fails fast with 503 `identity store unavailable` — never hangs, never falls back to anonymous or remote (FR-011).

## R8 — Operator CLI verb

**Decision**: `raggit user add --username <name> --display-name "<Full Name>" --role Admin|Employee [--password-stdin]`, implemented as an argument intercept at the top of `RAGGit.Workstation.Api/Program.cs` (before `builder.Build()`), writing directly to `data/rag.db` via `RagDbContext` + `PasswordHasher`. No HTTP, no running server, no first-run wizard (FR-004, spec Q3). Password read from stdin/interactive prompt by default; `--password` accepted for scripting with a documented process-list exposure warning. Exit codes: 0 ok · 2 duplicate username · 3 invalid role/args.

**Rationale**: the operator's OS-level access is the provisioning trust anchor (Assumptions); a separate CLI project would violate VII's single-project rule.
**Rejected**: first-run in-app admin wizard (explicitly rejected, Q3); seeding a secret into appsettings (plaintext in config — violates FR-004).

## R9 — Diagram handling (PlantUML since 2026-09-24)

**Decision**: diagrams are PlantUML `.puml` sources with rendered `.svg` in `docs/images/` (map in `docs/images/README.md`). Component names are stable across revisions and serve as the code↔diagram mapping contract for `/speckit.tasks` and coder.

## R10 — Username uniqueness & case handling

**Decision**: `Username` stored as typed; uniqueness enforced **case-insensitively** via SQLite `COLLATE NOCASE` unique index; authentication lookups case-insensitive (spec edge case "duplicate / username case handling"). Display form is preserved for the `me` envelope and Admin screen.

## Assumptions Register (spec Assumptions carried + new)

- Password policy v1: min 10 chars, no composition rules (documented assumption; tunable constant).
- Accounts are never hard-deleted (attribution references); deactivation is the off switch.
- Reactivation by Admin is supported (`PATCH /api/users/{id} {isActive:true}`) — not contradicted by spec, keeps the model total.
- `MustChangePassword` is honored client-side at next sign-in (edge case "admin reset optionally forces change"); server exposes the flag additively.