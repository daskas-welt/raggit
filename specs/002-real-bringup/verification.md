# Verification Log: Real Workstation Bring-Up

**Feature**: `002-real-bringup` | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

> Fill this log once on the developer's own laptop (SC-001/SC-002 measured wall-clock). The "<7s on reference workstation" figure is a **production assumption to verify at deploy**, not asserted on the dev laptop.

## Environment

| Field | Value |
|-------|-------|
| Date | YYYY-MM-DD |
| Operator | name / handle |
| Dev laptop model | e.g., ThinkPad X1, MBA M2, etc. |
| OS | e.g., Windows 11 23H2 / Ubuntu 22.04 |
| CPU | e.g., i5-1240P / M2 8-core |
| RAM | e.g., 16GB (or 8GB) |
| Disk | e.g., NVMe 512GB, free 40GB |
| .NET | `dotnet --version` |
| Ollama | `ollama --version` + `ollama list` |
| Embed model | `all-minilm` 384 (dev) — prod baseline `nomic-embed-text` 768 at deploy |
| Chat model | `phi3:mini` (dev) — prod baseline `llama3.2:3b` at deploy |
| Workstation | `http://localhost:5001` (dev) or `http://ai-workstation.local:5001` |
| LanceDB path | `./data/lancedb` (fresh = yes/no) |
| VectorSize | 384 (dev) / 768 (prod) |
| Network | WAN on for pull, then WAN-off for SC-002 verification (LAN/localhost kept) |

## SC-001 — Upload 50-page PDF → Ready in <5 min (dev laptop)

| Field | Value |
|-------|-------|
| Sample PDF | filename, pages, size (MB), hash |
| Upload method | `curl -F file=@sample.pdf` or MAUI UploadView |
| Upload start (wall-clock) | HH:MM:SS |
| `POST /api/documents` response | 201 id=..., status Indexing |
| Poll `GET /api/documents` interval | e.g., 5s |
| Ready at (wall-clock) | HH:MM:SS |
| Wall time Ready | e.g., 47s |
| Result | PASS / FAIL (<300s) |
| Notes | |

Raw log:

```
# paste curl -v and polling timestamps here
```

## SC-002 — WAN-off query cited answer (dev laptop, prod assumption)

| Field | Value |
|-------|-------|
| Query | e.g., "refund policy" |
| API key role | Employee |
| WAN state | logically disabled (or ollama stopped for offline fail-fast) — LAN/localhost kept |
| `POST /api/query` start | HH:MM:SS.mmm |
| Response received | HH:MM:SS.mmm |
| Wall time (ms) | e.g., 18430 (dev laptop; do not assert <7000) |
| Status | 200 + citations[≥1] OR 200 no relevant content found |
| Answer snippet | first 200 chars |
| Citations | count, docIds |
| Result | PASS (returned, fail-fast when Ollama down is 503) |
| Notes | |

Raw log:

```
# paste curl timing and response here
# e.g., curl -w "@curl-format.txt" -X POST http://localhost:5001/api/query ...
```

**Production assumption note** (required): The SC-002 "<7s on reference workstation" figure from the spec is **not asserted on the dev laptop**. Record the dev-laptop wall time above and explicitly note: "Dev measurement is [X]ms on this laptop; '<7s on reference workstation' is a production assumption to verify at deploy on the prod box (16GB + GPU) with `nomic-embed-text` 768 + `llama3.2:3b` and a populated `library` collection."

## SC-003 — Opt-in real suite (zero fakes core path, skip when Ollama absent)

| Field | Value |
|-------|-------|
| `dotnet test --filter Trait=RequiresOllama` | PASS / SKIP (ollama absent, graceful) |
| `dotnet test` (fakes) | PASS (CI green) |
| Notes | |

## SC-004 — Dimension guard 384 ↔ 768

| Field | Value |
|-------|-------|
| Initial DB | VectorSize 384 + 1 doc indexed |
| Swap to | VectorSize 768 (`nomic-embed-text`) without wipe |
| Result | Startup fails fast with both dims + recovery (wipe data/lancedb) — PASS/FAIL |
| After wipe + re-ingest 768 | queries succeed — PASS/FAIL |
| Notes | |

## SC-005 — Corrupted pdf → 400, no partial index

| Field | Value |
|-------|-------|
| Sample | truncated/invalid pdf filename, size |
| `POST /api/documents` result | 400 {error: ...} — PASS/FAIL |
| `GET /api/documents` lists partial? | no — PASS/FAIL |
| Notes | |

## Sign-off

- [ ] SC-001 measured and <5 min on dev laptop
- [ ] SC-002 measured and wall time recorded (prod <7s noted as assumption, not asserted)
- [ ] SC-003 opt-in skipped gracefully when Ollama absent, CI green with fakes
- [ ] SC-004 guard fails fast and recovers after wipe
- [ ] SC-005 corrupted pdf 400 with no partial

Operator: ________________  Date: ________________
