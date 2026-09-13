# Verification: 005-per-person-history

Measured `2026-09-14` on `005-per-person-history` (branch head `409dd67` + T032);
quickstart steps 1–11 validated via the automated equivalents below
(contract/integration suites are the scripted form of steps 4–11).

| SC | Criterion (tech-agnostic) | Measurement procedure | Pass condition | Status |
|---|---|---|---|---|
| **SC-001** | Signed-in person with up to 1,000 queries loads first history page in <2s on LAN | `quickstart.md` step 4 timing: `COUNT(*)` + first page (`limit=20`) over a 1,000-row `Queries` table (500 owned / 500 other) using the shipped `ListSql`/`CountSql` (`QueryHistoryStore.cs:294`), 10 runs, Release | p95 < 2 s | ✅ PASS — p95 `3.3 ms` incl. cold-open run 1; steady-state `0.5–0.9 ms` (runs 2–10); `total=500 rows=20` every run |
| **SC-002** | ≥2 active persons ≥20 mixed actions, zero cross-user rows returned | `integration/CrossUserIsolationTests` — Admin + Employee, 24 mixed actions (22 queries: 10 admin + 12 employee, + 2 uploads), `GET /history` + `GET /{id}` assert own-only | 0 leak, 100% own-only | ✅ PASS — 7/7 isolation facts green, `0` foreign rows; suite `8/8` with `OfflineHistoryTests` (`6 s`) |
| **SC-003** | Pagination consistent `0,10 + 10,10 == 20` same order | `contract/HistoryContractTests` pagination test: `limit=10 offset=0` + `limit=10 offset=10` union equals `limit=20 offset=0`; `CreatedAt DESC, Id DESC`; disjoint check | exact match, disjoint | ✅ PASS — pagination/union fact green within contract run `21/21` (`7 s`) |
| **SC-004** | WAN-disabled history/docs same data/latency as WAN-enabled | `integration/OfflineHistoryTests` (LAN CI `unshare -n` fallback) — same seed, run history+mine both modes, compare `total/items` and wall time | same data, no degradation, 0 egress | ✅ PASS — byte-identical bodies WAN-on vs WAN-off (`admin 3/2`, `employee 2/1`); LLM sabotaged (`Healthy=false`, `ThrowOnChat=true`) with no effect (SQLite-only reads) |
| **SC-005** | Legacy admin/employee queries not in any person's history | `integration/CrossUserIsolationTests` legacy-exclusion subcase + `contract/HistoryContractTests` — insert legacy `UserId='admin'` row, assert not in either person's `GET /history` | not found | ✅ PASS — legacy `admin`/`employee` rows excluded from history, detail (`404`), and mine |

**Evidence paths after implement**: `tests/contract/HistoryContractTests.cs`, `tests/integration/CrossUserIsolationTests.cs`, `tests/integration/OfflineHistoryTests.cs`, `tests/unit/QueryHistoryStoreTests.cs` (optional unit for clamping/truncation).
