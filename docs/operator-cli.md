# Operator CLI

The operator CLI provisions the first person accounts directly on the AI Workstation. It writes to the local SQLite metadata database (`data/rag.db` by default) so the API does not need to be running, and it never stores a plaintext password in configuration or logs.

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

## Connection string override

To target a database other than the default `./data/rag.db`:

```powershell
dotnet RAGGit.Workstation.Api.dll user add ... --connectionstrings:RagDb "Data Source=/path/to/rag.db"
```
