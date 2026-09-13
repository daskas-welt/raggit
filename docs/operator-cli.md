# Operator CLI

The operator CLI provisions the first person accounts directly on the AI Workstation (1.3.0). It writes to the local SQLite metadata database (`data/rag.db` by default) so the API does not need to be running, and it never stores a plaintext password in configuration or logs. After provisioning, people sign in with username/password over HTTPS via `POST /api/auth/login` or the MAUI `LoginView`.

## Usage

```powershell
# Run from the workstation (where the API assembly is deployed):
dotnet RAGGit.Workstation.Api.dll user add `
  --username <username> `
  --display-name "Display Name" `
  --role Admin|Employee `
  --password <password>

# Or read the password from stdin (recommended for scripts):
"$password" | dotnet RAGGit.Workstation.Api.dll user add `
  --username bob `
  --display-name "Bob Moore" `
  --role Employee `
  --password-stdin
```

## Exit codes

| Code | Meaning |
|------|---------|
| `0`  | User created successfully. |
| `1`  | Usage error or missing required argument. |
| `2`  | Duplicate username (case-insensitive). |
| `3`  | Invalid role (must be `Admin` or `Employee`). |

## Examples

```powershell
# Provision an Admin and an Employee on a fresh workstation:
dotnet RAGGit.Workstation.Api.dll user add --username ada --display-name "Ada Lovelace" --role Admin --password-stdin
dotnet RAGGit.Workstation.Api.dll user add --username bob --display-name "Bob Moore" --role Employee --password-stdin
```

## Security notes

- Passwords are hashed with PBKDF2-SHA256 (310,000 iterations, 16-byte salt) before storage.
- The hash format is self-describing: `PBKDF2-SHA256$310000$<salt>$<hash>`.
- No plaintext password is written to `appsettings*.json`, environment files, or the database.
- The command can be run offline; the workstation does not contact any cloud identity provider.

## Per-person history (1.4.0 additive, no breaking change)

`005-per-person-history` adds three read-only endpoints scoped to the caller's JWT
`sub` (see `specs/005-per-person-history/contracts/api.yaml`). No operator action is
needed — history is a query-layer projection over the existing `Queries`/`Documents`
tables attributed by `004-identity`; no migration, no new tables.

- `GET /api/queries/history?limit=&offset=` — own queries only
  (`UserId == sub AND NOT IN ('admin','employee')`, `CreatedAt DESC, Id DESC`,
  `limit` default 20 / max 100 clamped, `offset` default 0). `401` without a person
  session (legacy API-key callers carry no `sub`); `200` with `items:[] total:0`
  for new users.
- `GET /api/queries/{id}` — full `prompt/answer/citations[]` iff owned and not
  legacy. `404` when not found, not owned, or legacy (`404`, never `403`, so
  non-owners cannot enumerate query ids); `401` without a session.
- `GET /api/documents/mine?limit=&offset=` — own uploads only
  (`CreatedBy == sub AND NOT IN ('admin','employee')`), same envelope/clamping.
  Admins see only their own rows — no cross-user view in v1.

Isolation spot-check (after provisioning `ada`/`bob` per step 1 of
`specs/005-per-person-history/quickstart.md`):

```powershell
$ada = (Invoke-RestMethod -Method Post -Uri https://localhost:5001/api/auth/login -Body '{"username":"ada","password":"<ada-pw>"}' -ContentType application/json).access_token
$bob = (Invoke-RestMethod -Method Post -Uri https://localhost:5001/api/auth/login -Body '{"username":"bob","password":"<bob-pw>"}' -ContentType application/json).access_token
Invoke-RestMethod -Headers @{Authorization="Bearer $bob"} https://localhost:5001/api/queries/history?limit=20&offset=0
Invoke-RestMethod -Headers @{Authorization="Bearer $bob"} https://localhost:5001/api/documents/mine?limit=20&offset=0
```

Expect each token returns only its own rows (`0` leak); legacy `UserId='admin'` /
`'employee'` rows never appear; all three endpoints work WAN-disabled (SQLite-only
reads, verified by `OfflineHistoryTests`).

## Connection string override

To target a database other than the default `./data/rag.db`:

```powershell
dotnet RAGGit.Workstation.Api.dll user add ... --connectionstrings:RagDb "Data Source=/path/to/rag.db"
```
