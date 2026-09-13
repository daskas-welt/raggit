# Verification: 005-per-person-history

Each SC is measured by a named procedure; ☐ = not yet run (coder executes during `/speckit.tasks`/`/speckit.implement`).

| SC | Criterion (tech-agnostic) | Measurement procedure | Pass condition | Status |
|---|---|---|---|---|
| **SC-001** | Signed-in person with up to 1,000 queries loads first history page in <2s on LAN | `quickstart.md` step 4 timing: `Measure-Command` around `GET /api/queries/history?limit=20&offset=0` with seeded 1k rows per `CrossUserIsolationTests`/`HistoryContractTests`; repeat 10 runs | p95 < 2 s | ☐ |
| **SC-002** | ≥2 active persons ≥20 mixed actions, zero cross-user rows returned | `integration/CrossUserIsolationTests` — 2 users (Admin + Employee) each queries + uploads, assert `GET /history` and `GET /documents/mine` each returns only rows with `UserId/CreatedBy == sub`; no row from other person | 0 leak, 100% own-only | ☐ |
| **SC-003** | Pagination consistent `0,10 + 10,10 == 20` same order | `contract/HistoryContractTests` pagination test: `limit=10 offset=0` + `limit=10 offset=10` union equals `limit=20 offset=0` items; order `CreatedAt DESC, Id DESC`; disjoint check | exact match, disjoint | ☐ |
| **SC-004** | WAN-disabled history/docs same data/latency as WAN-enabled | `integration/OfflineHistoryTests` (LAN CI `unshare -n` fallback) — same seed, run history+mine both modes, compare `total/items` and wall time | same data, no degradation, 0 egress | ☐ |
| **SC-005** | Legacy admin/employee queries not in any person's history | `integration/CrossUserIsolationTests` legacy-exclusion subcase + `contract/HistoryContractTests` — insert legacy `UserId='admin'` row, assert not in either person's `GET /history` | not found | ☐ |

**Evidence paths after implement**: `tests/contract/HistoryContractTests.cs`, `tests/integration/CrossUserIsolationTests.cs`, `tests/integration/OfflineHistoryTests.cs`, `tests/unit/QueryHistoryStoreTests.cs` (optional unit for clamping/truncation).
