# Quickstart & Validation — 004-identity (Per-Person Identity & Accounts)

**Date**: 2026-09-13 | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Contract**: [contracts/api.yaml](./contracts/api.yaml) (1.3.0) | **Diagrams**: [docs/architecture.html](./docs/architecture.html?theme=light) → [docs/workflow.html](./docs/workflow.html?theme=light) → [docs/sequence.html](./docs/sequence.html?theme=light)

Seven end-to-end validation steps. Each maps to a user story, FRs, and SCs; the same flows are automated in `tests/contract/` and `tests/integration/` (see [verification.md](./verification.md)). Prerequisites: built `RAGGit.Workstation.Api` + `RAGGit.Client.Maui`, workstation `data/` directory, and a client machine that trusts the workstation certificate (step 2a).

---

## 1. Operator CLI provisions the first people (US1 · FR-002/FR-004 · workflow `cli-provision → seed-users`)

On the workstation (OS trust anchor — no first-run wizard). See also [`docs/operator-cli.md`](../../docs/operator-cli.md) for full usage, exit codes, and security notes.

```powershell
# aliased as `raggit` in deployment docs; runs against data/rag.db directly, server need not be up
dotnet RAGGit.Workstation.Api.dll user add --username ada --display-name "Ada Lovelace" --role Admin --password-stdin
dotnet RAGGit.Workstation.Api.dll user add --username bob --display-name "Bob Moore" --role Employee --password-stdin
```

**Verify**:
- `Users` table has 2 rows; `PasswordHash` values match `PBKDF2-SHA256$310000$…` (salted, non-recoverable).
- No plaintext password in `appsettings*.json`, the DB, or logs (FR-002/FR-004).
- Start the API: it boots with zero accounts required and presents **no** in-app "create admin" flow (US1.2).
- Duplicate username (any case) exits 2; invalid role exits 3.

## 2. HTTPS sign-in (US2 · FR-002/003/008 · SC-001/SC-004 · sequence "Provision + Login")

**2a. Trust the cert (client machine, one-time)**: dev — `dotnet dev-certs https --trust`; production — operator installs the internal-CA/self-signed workstation cert into Trusted Root. If untrusted, the client shows a clear actionable error and **never** bypasses validation (R3).

```bash
curl -s https://ai-workstation.local:5001/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"username":"bob","password":"<bob-password>"}'
```

**Verify**:
- `200` → `{access_token, token_type:"Bearer", expires_in:28800}` (8 h, FR-003).
- Wrong password and unknown username return the **identical** generic `401 {"error":"unauthorized"}` — no enumeration (US2.3).
- Wire capture (SC-004): password and token appear **only** inside TLS; plain HTTP auth requests are redirected/refused.
- Sign-in → library reachable in < 10 s on LAN (SC-001).

## 3. Identity discovery — `me` envelope (US2 · FR-006/FR-013 · sequence "200 Local/Admin")

```bash
curl -s https://ai-workstation.local:5001/api/auth/me -H "Authorization: Bearer $TOKEN"
```

**Verify**: `200` → `{"identityType":"Local","role":"Employee","displayName":"Bob Moore","username":"bob","sub":"<user-uuid>"}`.
**Back-compat (FR-013)**: an `X-Api-Key` call still returns the 1.2.0 shape (`identityType:"ApiKey"`, `role` only) and 1.2.0 clients ignore the new fields.

## 4. Upload + query attribution (US2 · FR-007 · SC-002 · dataflow `client-cache → docs/queries`)

- As **ada** (Admin Bearer): upload a PDF via client or `POST /api/documents` → `createdBy == ada's sub` (uuid).
- As **bob** (Employee Bearer): `POST /api/query` → `Queries.UserId == bob's sub`; answer carries citations (V unchanged).
- **Verify (SC-002)**: 100 % of session actions attributed to the person's stable id; zero shared-key attribution for person actions. Legacy rows with `"admin"/"employee"` remain untouched.
- **Verify (US3.3)**: bob (Employee) attempting upload → `403`.

## 5. Admin manages people in-app (US3 · FR-005/FR-010 · SC-003/SC-006 · workflow `admin-manage → deactivate-lockout`)

As ada, via the client's **Admin → Users** screen (`SfDataGrid`) or API:

1. `POST /api/users` create `carol` (Employee) → carol signs in immediately (US3.1, SC-006 — no restart).
2. `PATCH /api/users/{carol-id}` `{"role":"Admin"}` → effective immediately.
3. `PATCH` `{"isActive":false}` → carol's **very next request** on her still-unexpired token returns `401` (FR-010, SC-003); sign-in refused with generic 401.
4. `POST /api/users/{carol-id}/reset-password` (+`mustChangePassword:true`) → client prompts password change at next sign-in.
5. Reactivate `{"isActive":true}` → sign-in works again.
6. As bob (Employee): `GET /api/users` → `403` (US3.3).

## 6. Lockout (edge cases · FR-009 · R5)

```bash
for i in 1 2 3 4 5; do curl -s -o /dev/null -w '%{http_code}\n' https://ai-workstation.local:5001/api/auth/login \
  -H 'Content-Type: application/json' -d '{"username":"bob","password":"wrong"}'; done
```

**Verify**: attempts 1–5 → `401` (generic); attempt 6 (while locked) → `429` with `Retry-After`; `Users.LockoutUntil` set ~15 min ahead. After expiry (or admin password reset), correct credentials succeed and `FailedAccessCount` resets. Lockout survives an API process restart (DB-persisted).

## 7. Offline proof (FR-011 · SC-005 · Constitution IV · research R7)

Disable WAN on the workstation (LAN stays up). Repeat steps 2 → 4: **login, me, upload, and a cited query all succeed end-to-end**; zero outbound connections beyond LAN/loopback (packet capture or existing socket-assert harness). Stop/rename `data/rag.db` → auth endpoints fail fast `503 identity store unavailable` — no hang, no anonymous fallback, no remote IdP contact.

---

**Client session lifecycle (US4)** during any step: kill the app and relaunch → session restored from SecureStorage without re-typing (until expiry); expired token → re-prompt (no silent shared-key fallback); sign-out → cache cleared, subsequent calls anonymous (`401`).

**CI mapping**: steps 1–7 automate as `tests/unit/PasswordHasherTests`, `tests/contract/AuthLoginContractTests|UsersCrudContractTests|AuthMeAdditiveContractTests`, `tests/integration/IdentityAttributionTests|DeactivationRefusalTests|OfflineIdentityTests` (WAN-disabled leg).
````

---

## 6. `specs/004-identity/verification.md` (NEW)

````markdown