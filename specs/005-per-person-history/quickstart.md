# Quickstart: 005-per-person-history

**Feature**: Per-Person Query & Document History — `specs/005-per-person-history/spec.md` — `contracts/api.yaml 1.4.0`

## Prerequisites

- `main` at `v1.3.0` (`004-identity` merged) with `PBKDF2/JWT/Admin CRUD` working
- `dotnet 8`, `data/rag.db` writable, `LanceDB` at `./data/lancedb` (not used at query-time history)
- Existing `TestApiFactory` pattern for contract/integration tests

## 1. Provision two people (operator CLI, reuse 004)

```powershell
dotnet run --project src/RAGGit.Workstation.Api -- user add --username ada --display-name "Ada" --role Admin --password "S3cretAda!" 
dotnet run --project src/RAGGit.Workstation.Api -- user add --username bob --display-name "Bob" --role Employee --password "S3cretBob!"
```

Verify: `SELECT Id,Username,Role FROM Users;` — two rows, `PasswordHash` starts `PBKDF2-SHA256$310000$…`

## 2. Sign in each person and capture tokens

```powershell
$ada = (Invoke-RestMethod -Method Post -Uri https://localhost:5001/api/auth/login -Body '{"username":"ada","password":"S3cretAda!"}' -ContentType application/json).access_token
$bob = (Invoke-RestMethod -Method Post -Uri https://localhost:5001/api/auth/login -Body '{"username":"bob","password":"S3cretBob!"}' -ContentType application/json).access_token
```

Verify: `GET /api/auth/me` with `Authorization: Bearer $bob` returns `identityType:"Local", username:"bob", sub:"<uuid>"`.

## 3. Create attribution data (queries + uploads)

Upload as each person (via MAUI client or `curl -F file=@paper.pdf -H "Authorization: Bearer $token" https://localhost:5001/api/documents`), then query:

```powershell
Invoke-RestMethod -Method Post -Uri https://localhost:5001/api/query -Headers @{Authorization="Bearer $bob"} -Body '{"prompt":"What does the paper say about offline RAG?"}' -ContentType application/json
Invoke-RestMethod -Method Post -Uri https://localhost:5001/api/query -Headers @{Authorization="Bearer $ada"} -Body '{"prompt":"Summarize the upload policy"}' -ContentType application/json
```

Repeat ≥10 per person for `SC-003` later.

## 4. History isolation — own queries only (FR-001/FR-007, SC-002)

```powershell
$bobHist = Invoke-RestMethod -Headers @{Authorization="Bearer $bob"} https://localhost:5001/api/queries/history?limit=20&offset=0
$adaHist = Invoke-RestMethod -Headers @{Authorization="Bearer $ada"} https://localhost:5001/api/queries/history?limit=20&offset=0
```

Expect: `$bobHist.total == bob query count`, `items[*].promptPreview` truncated ≤121 chars, `citationCount` matches stored, `createdAt` descending, `0` rows from other person. Repeat with roles swapped — Admin `ada` still sees only own.

## 5. Query detail with citations (FR-004)

```powershell
$id = $bobHist.items[0].id
Invoke-RestMethod -Headers @{Authorization="Bearer $bob"} https://localhost:5001/api/queries/$id
Invoke-RestMethod -Headers @{Authorization="Bearer $ada"} https://localhost:5001/api/queries/$id  # expect 404 not owned
```

Expect: `200` with full `prompt/answer/citations[]` for owner, `404` for non-owner, `404` for non-existent, `401` without token.

## 6. Recent documents mine (FR-005)

```powershell
Invoke-RestMethod -Headers @{Authorization="Bearer $bob"} https://localhost:5001/api/documents/mine?limit=20&offset=0
Invoke-RestMethod -Headers @{Authorization="Bearer $ada"} https://localhost:5001/api/documents/mine?limit=20&offset=0
```

Expect: each sees only `CreatedBy == sub` rows; `0` leak; new user sees `items:[] total:0`.

## 7. Pagination stability (FR-010, SC-003)

```powershell
$a = Invoke-RestMethod -Headers @{Authorization="Bearer $bob"} https://localhost:5001/api/queries/history?limit=10&offset=0
$b = Invoke-RestMethod -Headers @{Authorization="Bearer $bob"} https://localhost:5001/api/queries/history?limit=10&offset=10
$c = Invoke-RestMethod -Headers @{Authorization="Bearer $bob"} https://localhost:5001/api/queries/history?limit=20&offset=0
# assert ($a.items + $b.items).id == $c.items.id and disjoint
```

Expect: stable `CreatedAt DESC, Id DESC` — `a.id ∪ b.id == c.id`, `a ∩ b == ∅`.

## 8. Legacy exclusion (FR-008, SC-005)

Insert legacy ApiKey query: `INSERT INTO Queries(Id,UserId,Prompt,Answer,CreatedAt,LatencyMs) VALUES ('legacy1','admin','…','…',…)` then:

```powershell
Invoke-RestMethod -Headers @{Authorization="Bearer $bob"} https://localhost:5001/api/queries/history?limit=100&offset=0 | % { $_.items | ? { $_.id -eq 'legacy1' } }
```

Expect: not found in any person's history.

## 9. Offline history (FR-009, SC-004)

Use LAN CI gate: `unshare -n dotnet test --filter OfflineHistoryTests` (fallback probe as in `.github/workflows/ci.yml`). Verify history/docs return same `total/items` WAN-disabled.

## 10. Unauthenticated. (FR-006)

```powershell
Invoke-RestMethod https://localhost:5001/api/queries/history -ErrorAction SilentlyContinue | Select-Object StatusCode
```

Expect: `401`.

## 11. Contract coverage

```powershell
dotnet test tests/contract --filter HistoryContractTests
dotnet test tests/integration --filter CrossUserIsolationTests
dotnet test tests/integration --filter OfflineHistoryTests
```

Expect: `100%` history contracts, `0` leak over `≥2 users ≥20 actions`.

References: `spec.md` US1-US5, `data-model.md` (projection queries), `contracts/api.yaml` (1.4.0), `research.md` R1-R7.
