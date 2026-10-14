---
description: "Task list for Remote Workstation Hosting (031)"
---

# Tasks: Remote Workstation Hosting

**Input**: Design documents from `/specs/031-remote-workstation-hosting/`

**Prerequisites**: [plan.md](plan.md) (required), [spec.md](spec.md) (required for user stories), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)

**Tests**: No new application test tasks are generated — the REST contract and RAG logic do not change (plan [R-11](research.md)), so the existing unit/contract/offline/integration suites are the regression net (T026). Feature acceptance is proven by the deployment runbook scenarios in [quickstart.md](quickstart.md), executed as the verification tasks below. One new automated check is added for the deploy artifacts themselves (T027).

**Organization**: Tasks are grouped by user story. This is a deployment/ops feature with two owners: **coder** for checked-in repository artifacts and docs, and **deployer** for live cloud infrastructure (its write scope is `deploy/*` and `docs/*`). The feature supports **two deployment modes** (local default; private cloud reachable only over the company's private network) from one codebase (FR-018).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US4). Setup, Foundational, Local Mode Parity, and Polish tasks carry no story label.
- Every task names its exact file path or command target

## Path Conventions

Paths are repository-relative. New artifacts live under `deploy/` and `docs/runbooks/` per [plan.md](plan.md) Project Structure; local mode additionally reuses the existing root `docker-compose.yml`. The REST API, Dockerfile, and client config mechanism are reused unchanged.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the deployment artifact scaffolding that every later phase reads.

- [x] T001 Create the `deploy/` directory and add `deploy/.env` to `.gitignore` so only `.env.example` is committed (contracts/deployment.md §2 secret rule)
- [x] T002 [P] Create `.dockerignore` at the repository root excluding `.git/`, `**/bin/`, `**/obj/`, `specs/`, `tests/`, `.opencode/`, `.codegraph/`, `data/` so the Dockerfile's `COPY . .` sends a small context
- [x] T003 [P] Create `deploy/.env.example` listing every variable in [contracts/deployment.md](contracts/deployment.md) §2 (`WORKSTATION_HOST`, `RAGGIT_DATA_DIR`, `ADMIN_API_KEY`, `EMPLOYEE_API_KEY`, `CERT_PFX_PATH`, `CERT_PFX_PASSWORD`, `EMBED_MODEL`, `CHAT_MODEL`, `VECTOR_SIZE`, `OLLAMA_URL`) with placeholder values, and state each one's container-env/config-key mapping (so `.env` names are never mistaken for .NET keys); no real secrets
- [x] T004 [P] Create `deploy/appsettings.client.template.json` per [contracts/deployment.md](contracts/deployment.md) §5 — `Workstation:Url` = `https://<WORKSTATION_HOST>`, `Workstation:ApiKey` = `<EMPLOYEE_API_KEY>`, `Workstation:UploadConcurrency` = 4 — with a comment noting that a URL without a key yields the `invalid-config` client

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The thing being deployed. No user story can be verified until the workstation is running and healthy in the chosen mode.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [x] T005 [P] Create `deploy/docker-compose.prod.yml` (private cloud mode): build the existing `src/RAGGit.Workstation.Api/Dockerfile`; set `ASPNETCORE_URLS=https://0.0.0.0:5001` and the Kestrel cert path/password; map `.env` values to their literal container env vars per [contracts/deployment.md](contracts/deployment.md) §2 (`Api__AdminKey`, `Api__EmployeeKey`, `Ollama__Url`, `Ollama__EmbedModel`, `Ollama__ChatModel`, `VectorDb__VectorSize`, `VectorDb__Path=/app/data/lancedb`); publish `443:5001` **bound to the private interface** (never a public `0.0.0.0`); bind-mount `$RAGGIT_DATA_DIR:/app/data`; keep `extra_hosts: host.docker.internal:host-gateway`; `restart: unless-stopped`; publish no Ollama port
- [x] T006 [P] Create `docs/runbooks/workstation-deployment.md` covering **both modes**: prerequisites/credentials; local-mode stand-up using the root `docker-compose.yml`; private-cloud provision (machine + volume + private network address), mount the volume, issue a certificate whose SAN matches the address clients use (IP SAN for a private IP, else the VPN host name; publicly trusted only if a public name exists), restrict reachability, run `deploy/bootstrap.sh`, verify `GET https://<host>/health`, and confirm no response from outside the company's networks (research.md R-01/R-02/R-03/R-07)
- [x] T007 Create `deploy/bootstrap.sh` (bash, run over SSH for private cloud mode): install Docker and native Ollama, mount the volume, place `deploy/docker-compose.prod.yml` + `.env`, `docker compose up -d`, `ollama pull $EMBED_MODEL` and `$CHAT_MODEL`, and create the first admin account via the operator CLI inside the container (exact invocation: `dotnet RAGGit.Workstation.Api.dll user add --username … --display-name … --role Admin --password …`, run from the API directory so the DB path resolves); idempotent to re-run (research.md R-09)
- [x] T008 Constitution amendments **applied 2026-10-14** with owner approval — `1.3.0 → 1.4.0`: Principle I widened to "Single-Tenant On-Prem or Private Cloud" (workstation never an open public endpoint), Technology & Deployment Constraints refreshed to the current stack (.NET 10 / LanceDB / native Ollama) and restated Network wording, Principle II runtime reference aligned. `1.4.0 → 1.5.0`: Principle IV's verification clause scoped so private cloud mode verifies the **workstation-local** query offline and the client↔workstation transport is out of scope. Constitution is now v1.5.0; see `.specify/memory/constitution.md` § Amendments 1.3.0 → 1.4.0 and 1.4.0 → 1.5.0.
- [ ] T009 Provision the private cloud environment: a CPU-only virtual machine (baseline 4 vCPU / 8 GB per research.md R-08), a block-storage volume, a private-network address (VPC/VPN), and a reserved/private IP; record IDs, addresses, and estimated cost — **owner: deployer**
- [ ] T010 Attach the volume, mount it on the host at `RAGGIT_DATA_DIR`, and restrict reachability: inbound tcp 443 only from the company's VPN/office source ranges, tcp 22 only from operator source ranges, everything else denied; publish no Ollama port (research.md R-07; FR-005, FR-016) — **owner: deployer**
- [ ] T011 Deploy the stack on the machine using T005–T007 and verify `GET https://<WORKSTATION_HOST>/health` returns HTTP 200 with `vectorDb: ok` and `llm: ok` from a company-network machine, **and** that the same request from outside the company's networks fails (FR-006, FR-012; research.md R-10) — **owner: deployer**

**Checkpoint**: The workstation is serving over HTTPS, healthy from inside, unreachable from outside — user story verification can begin.

---

## Phase 3: User Story 1 - Reach the workstation from a desktop and sign in (Priority: P1) 🎯 MVP

**Goal**: An employee on a Windows PC (UI local) opens the desktop client pointed at the workstation - local or private cloud - and signs in as themselves, with no AI or library state on the PC.

**Independent Test**: With the workstation healthy (Phase 2), copy `deploy/appsettings.client.template.json` to a client's `appsettings.json`, install the certificate into Trusted Root, and confirm a real account sign-in reaches the signed-in home screen.

- [x] T012 [US1] Append a "Client configuration" section to `docs/runbooks/workstation-deployment.md`: copy the template to the deployed client's `appsettings.json` (LAN host in local mode, private/VPN host in private cloud mode), install the company/self-signed certificate to **Trusted Root Certification Authorities**, and state that changing the file needs no rebuild (FR-015; contracts/deployment.md §5)
- [ ] T013 [US1] Verify sign-in end-to-end from a Windows client against the deployed workstation and record elapsed time from fresh machine to first successful sign-in (< 60 minutes, SC-001)
- [ ] T014 [US1] Verify the negative paths: unreachable `Workstation:Url` shows a clear "cannot reach AI workstation" error within 30 seconds (no hang); removing `Workstation:ApiKey` yields the `invalid-config` client; editing only `appsettings.json` switches the target with no reinstall (FR-004, FR-014, FR-015; SC-002, SC-006)

**Checkpoint**: US1 works independently — a signed-in desktop session against the workstation.

---

## Phase 4: User Story 2 - Upload documents to the workstation (Priority: P1)

**Goal**: Documents uploaded from a desktop client reach the workstation, are indexed there, and reach Ready (or Failed with a readable reason).

**Independent Test**: From a signed-in client, upload a supported document and confirm it settles to Ready and appears in the library.

- [ ] T015 [US2] Verify an upload from the desktop client advances `Uploading → Queued → Indexing → Ready` on the workstation and is visible in the client (FR-009; SC-003)
- [ ] T016 [US2] Verify an intentionally corrupt/unextractable document is accepted and settles to `Failed` with a user-safe `Document.FailureReason` (FR-009)
- [ ] T017 [US2] Confirm the ingested artifacts (`lancedb/`, `rag.db`, `documents/`, `auth.key`) exist under the workstation's data directory (the mounted volume in private cloud mode) and that no model, index, or document store was created on the PC (FR-002; data-model.md data contract)

**Checkpoint**: US1 and US2 both work — the library can be populated from a desktop.

---

## Phase 5: User Story 3 - Ask questions and receive cited answers (Priority: P2)

**Goal**: Queries answered from the workstation's library return citation-grounded answers or exactly "no relevant content found".

**Independent Test**: Ask a question covered by an uploaded document and one that is not, and confirm a cited answer and the exact no-content response respectively.

- [ ] T018 [US3] Verify a query whose answer is in an uploaded document returns an answer carrying `[chunk-GUID]` citations to the workstation's sources (FR-010; SC-004)
- [ ] T019 [US3] Verify a query with no matching content returns exactly `no relevant content found` and no fabricated text (FR-010; SC-004)
- [ ] T020 [US3] Confirm no external AI egress occurs while answering (generation and embedding served by the workstation's local models) (FR-011)

**Checkpoint**: The full upload-and-query value loop works against the workstation.

---

## Phase 6: User Story 4 - Operate and maintain the workstation (Priority: P3)

**Goal**: The operator can observe health, preserve the library across rebuilds, and tear down compute without losing data.

**Independent Test**: Snapshot the deployment, rebuild the machine, and confirm the same accounts sign in and the same documents remain queryable.

- [x] T021 [US4] Append a "Health & monitoring" section to `docs/runbooks/workstation-deployment.md` documenting the liveness check (`GET /health` over HTTPS, `vectorDb`/`llm` = `ok`/`down`) and how to run it from inside the company's network (FR-012)
- [ ] T022 [US4] Verify snapshot → machine rebuild (private cloud mode: volume restored to a new machine in the private network; local mode: data directory restored) → all previously committed documents and accounts remain usable with no re-ingest (FR-007, FR-013; SC-005)
- [ ] T023 [US4] Verify teardown (private cloud mode: snapshot then delete the machine) leaves no billable compute running while the data copy remains restorable, and append the restore/teardown steps to `docs/runbooks/workstation-deployment.md` (FR-013; SC-009)

**Checkpoint**: All four user stories are independently functional.

---

## Phase 7: Local Mode Parity (cross-cutting)

**Purpose**: Local mode is the default (FR-018) and reuses existing artifacts, so it needs no new code — but it must be documented as a first-class mode and proven end to end (SC-010). This phase is cross-cutting, so its tasks carry no user-story label; it can run in parallel with Phases 3–6.

- [x] T024 [P] Document the local mode in `docs/runbooks/workstation-deployment.md`: run the existing root `docker-compose.yml` (or the API directly) on the on-network machine, point clients at the LAN address, no cloud account, and how the data directory is preserved and restored
- [ ] T025 Verify local mode end to end on a LAN: workstation running on the on-network machine, a desktop client on the same network signs in, uploads a document to `Ready`, and gets a cited answer (FR-018; SC-010)

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Regression proof, deploy-artifact verification, documentation consistency, and security confirmation across both modes.

- [ ] T026 [P] Run the unchanged local regression suite and confirm counts match the AGENTS.md baseline: `dotnet build RAGGit.Server.slnf -c Release`, then unit/contract/offline-filtered integration tests (`QueryOfflineTests|OfflineIdentityTests|OfflineHistoryTests|CrossUserIsolationTests`) — expected unit 339 / contract 86 / integration 62 with Ollama up, `RequiresOllama` skips honored
- [x] T027 [P] Add a cheap static check for the new deploy artifacts (research.md R-11; analysis CN2): validate `deploy/docker-compose.prod.yml` with `docker compose config` so YAML typos and `.env` interpolation breakage fail fast (runs locally/in CI, no live host). The live idempotency smoke was dropped by owner decision; `deploy/bootstrap.sh` is verified by the runbook steps instead (plan.md Constitution Check, Principle VI)
- [x] T028 [P] Update `AGENTS.md` with the two deployment modes, the `deploy/` artifact layout, the client-configuration gotcha (a URL without `Workstation:ApiKey` is invalid), and where the runbook lives
- [x] T029 [P] Add a pointer to `deploy/` and `docs/runbooks/workstation-deployment.md` from `README.md`; keep the root `docker-compose.yml` LAN note intact (it is now the local-mode deployment)
- [ ] T030 Verify SC-008 concurrency behaviour: no concurrency limit is enforced, and simultaneous uploads and queries complete with zero failed uploads and zero unanswered queries (the demo target is a single user)
- [ ] T031 Run the [quickstart.md](quickstart.md) scenarios end to end for the mode(s) deployed and capture evidence for every success criterion (SC-001…SC-010)
- [ ] T032 Security confirmation (both modes): only the client-facing service is reachable and only from the company's own networks; Ollama `11434` is not published; the workstation is not reachable from outside the company's networks; no secret appears in the repo, the runbook, or command output (FR-005, FR-016; SC-007)
- [ ] T033 [P] Re-confirm single-tenant on the deployed surface (FR-017): inspect the exposed route/OpenAPI surface and confirm no tenant or `company_id` parameter exists and the singleton Library is unchanged, so one deployment serves exactly one company

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately.
- **Foundational (Phase 2)**: Depends on Setup — **BLOCKS all user stories** (nothing can be verified until a workstation is serving).
- **User Stories (Phases 3–6)**: Depend on Foundational. US2 depends on US1's client configuration; US3 depends on US2's ingested content; US4 needs only the Phase 2 stack.
- **Local Mode Parity (Phase 7)**: Depends on Setup only (local mode reuses the root compose and needs no cloud); can run in parallel with Phases 3–6.
- **Polish (Phase 8)**: Depends on the desired phases being complete.

### User Story Dependencies

- **US1 (P1)**: After Foundational. No dependency on other stories.
- **US2 (P1)**: After Foundational and after US1 (needs a signed-in client to upload).
- **US3 (P2)**: After US2 (needs uploaded content to query).
- **US4 (P3)**: After Foundational only; can run in parallel with US1–US3 once the stack is up.

### Within Each Phase

- Artifacts before infrastructure: T005–T007 before T011.
- Provisioning before restriction before bring-up: T009 → T010 → T011.
- Governance: T008 (constitution amendment) was applied on 2026-10-14 with owner approval; no further governance gate remains before merge.
- Verification tasks run in listed order unless marked [P].

### Parallel Opportunities

- Phase 1: T002, T003, T004 in parallel after T001.
- Phase 2: T005, T006 in parallel; T007 after T005; T009 → T010 → T011; T008 in parallel with T005–T007.
- Phase 7: T024 in parallel with any Phase 3–6 task (different files); T025 runs where a LAN is available.
- Phase 8: T026, T028, T029, T030 in parallel.

---

## Parallel Example: Phase 1

```bash
Task: "T002 Create .dockerignore at the repository root"
Task: "T003 Create deploy/.env.example"
Task: "T004 Create deploy/appsettings.client.template.json"
```

## Parallel Example: Phase 2

```bash
Task: "T005 Create deploy/docker-compose.prod.yml"
Task: "T006 Create docs/runbooks/workstation-deployment.md"
Task: "T008 Constitution amendment 1.3.0 -> 1.4.0 (applied 2026-10-14)"
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Phase 1 Setup → Phase 2 Foundational (a workstation serving and healthy in the chosen mode).
2. Phase 3 (US1): configure a client and sign in.
3. **STOP and VALIDATE**: a signed-in desktop session against the workstation is the MVP.

Note: US1 is the MVP, but the feature's user-visible value needs US2 (upload) to be demonstrable; consider delivering US1 + US2 together as the first increment.

### Incremental Delivery

1. Setup + Foundational → workstation serving over HTTPS in the chosen mode.
2. US1 → sign-in from a desktop.
3. US2 → documents ingested on the workstation.
4. US3 → cited answers from the library.
5. US4 → snapshot/restore/teardown operations.
6. Local Mode Parity → the default mode proven end to end.
7. Polish → regression, deploy-artifact check, docs, security confirmation.

### Ownership Split

- **coder**: T001–T008, T012, T024, T026–T029 (repository artifacts and docs).
- **deployer**: T009–T011, plus the live execution behind T013–T023, T025, T030–T033. Per the deployer agent, it never edits `src/`, `tests/`, `.opencode/`, or `.specify/` — the constitution amendment (T008, now applied) was a coder/owner task, not a deployer one.

---

## Notes

- [P] tasks touch different files and have no dependencies on incomplete tasks.
- No application test tasks were generated: there is no new RAG/API behavior to TDD (research.md R-11). T027 adds a static `docker compose config` check for the compose file; `deploy/bootstrap.sh` is verified by the runbook steps (owner-accepted exemption), and the runbook scenarios are the acceptance evidence.
- T008 (constitution amendment) is applied (constitution v1.4.0); it stays in Phase 2 only as a historical record - it no longer blocks anything.
- Avoid editing the same runbook file from parallel tasks: T006, T012, T021, T023, and T024 all touch `docs/runbooks/workstation-deployment.md` and must run in the listed order.
- Only ONE mode is deployed at a time; the same codebase and artifacts serve both (FR-018, SC-010).
- Commit after each task or logical group.
