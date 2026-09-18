# Tasks: .NET 10 Upgrade

**Input**: Design documents from `/specs/014-net10-upgrade/` (spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md)
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/
**Tests**: No new tests — validation runs the existing suites (FR-002/SC-002). Test-run tasks are included per story.
**Organization**: Grouped by user story for independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story the task belongs to ([US1], [US2], [US3])

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Baseline capture so FR-001/SC-002 are verifiable after the change

- [x] T001 Record pre-upgrade warning count via `dotnet build RAGGit.Server.slnf -c Release` in repo root — baseline: 10 warnings (6×CS9057 OllamaSharp, CS1998, CS8600, CS8602, CS8604), 0 errors
- [x] T002 [P] Record pre-upgrade baselines (unit count + contract + integration pass counts) via `dotnet test` in repo root — unit 228/228, contract 78/78, integration 50/50 (1 flaky fail on first run, green on rerun), offline 12/12
- [x] T003 [P] Confirm SDK 10.0.401+ installed via `dotnet --list-sdks` in repo root — 8.0.425 + 10.0.401 present, active 8.0.425

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Runtime pins that MUST complete before ANY user story work

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T004 Update SDK pin to 10.0.401 in global.json
- [x] T005 Update default TFM to net10.0 in Directory.Build.props
- [x] T006 Verify `dotnet --version` resolves to 10.0.4xx in repo root — 10.0.401 ✓

**Checkpoint**: Foundation ready — `dotnet --version` prints 10.0.4xx; user story work can begin

---

## Phase 3: User Story 1 - Workstation runs on the supported runtime (Priority: P1) ⭐ MVP

**Goal**: Server, libraries, and tests build and pass on .NET 10; container image builds on 10.0

**Independent Test**: `dotnet build RAGGit.Server.slnf -c Release` (0 errors, warnings ≤ T001) + full test runs in T011–T014 + image builds and serves on port 5001 (quickstart.md steps 1–2, 5)

### Implementation for User Story 1

- [x] T007 [P] [US1] Update explicit TFM to net10.0 in tests/unit/RAGGit.Tests.Unit.csproj
- [x] T008 [P] [US1] Update explicit TFM to net10.0 in tests/contract/RAGGit.Tests.Contract.csproj
- [x] T009 [P] [US1] Update explicit TFM to net10.0 in tests/integration/RAGGit.Tests.Integration.csproj
- [x] T010 [US1] Bump base/SDK images 8.0 → 10.0 in src/RAGGit.Workstation.Api/Dockerfile (depends on T007–T009 for a coherent restore)
- [x] T011 [US1] Restore + Release build `RAGGit.Server.slnf` and compare warnings against T001 in repo root — 0 errors; code warnings 8→3 (CS9057×6, CS1998, CA2022 gone/fixed); new NU1903×3 advisories flag pre-existing transitive CVEs (follow-up, out of scope). Also: Mvc.Testing 8.0.10→10.0.12 required (old TestServer host fails on net10: PipeWriter.UnflushedBytes + boot flakes); fixed CA2022 in XlsxDeepValidationTests via ReadExactlyAsync; csharpier-formatted 2 WinUI files
- [x] T012 [US1] Run unit + contract suites via `dotnet test` in repo root (must match/beat T002) — unit 228/228, contract 78/78 (one flaky 6-fail batch, green ×3 after)
- [x] T013 [US1] Run WAN-disabled offline suites (query/identity/history/isolation, 0 leaks) via `dotnet test` in repo root — 12/12
- [x] T014 [US1] Run remaining integration suite via `dotnet test` in repo root — 50/50 (1 flake in 4 runs; also flaked once on net8 baseline → pre-existing)
- [x] T015 [US1] Run `dotnet csharpier check .` in repo root — clean (215 files)
- [ ] T016 [US1] Build workstation image and verify it serves on port 5001 via `docker compose` in repo root — BLOCKED: no docker daemon on this machine; tags updated, image build deferred to a docker host

**Checkpoint**: US1 fully functional — server builds, all suites green, image builds and serves; independently shippable as MVP (desktop client still on old runtime until US2)

---

## Phase 4: User Story 2 - Windows desktop client installs and runs unchanged (Priority: P1)

**Goal**: WinUI client builds on `net10.0-windows10.0.17763.0` + WindowsAppSDK 2.4.0 and packages an installable MSIX

**Independent Test**: Full-solution Windows build + unsigned MSIX gate artifact exists + manual smoke checklist passes (quickstart.md steps 3–4)

### Implementation for User Story 2

- [x] T017 [US2] Spike-validate WindowsAppSDK 2.4.0 on a scratch `winapp new` net10.0-windows project (record exact version; re-check NuGet for newer stable 2.x: stable, ≥2 weeks old, non-experimental) — validated via real build instead (branch-isolated): 2.4.0 chosen, 2.5.1 rejected as 2-days-old/0-downloads
- [x] T018 [US2] Bump Microsoft.Extensions.* 8.0.1 → 10.0.12 in src/RAGGit.Client.WinUI/RAGGit.Client.WinUI.csproj (depends on T017)
- [x] T019 [US2] Update TFM to net10.0-windows10.0.17763.0 in src/RAGGit.Client.WinUI/RAGGit.Client.WinUI.csproj (depends on T017)
- [x] T020 [US2] Bump Microsoft.WindowsAppSDK 1.5.240311000 → 2.4.0 in src/RAGGit.Client.WinUI/RAGGit.Client.WinUI.csproj (depends on T017)
- [x] T021 [US2] Fix any 1.5→2.x breaking-change build errors in src/RAGGit.Client.WinUI/ (depends on T018, T019, T020) — none: 0 errors
- [x] T022 [US2] Full-solution Release build `RAGGit.sln -p:Platform=x64` on Windows (depends on T021) — RAGGit.sln Release x64: 0 errors, 20 warnings (per-project NU1903 repeats + pre-existing set)
- [x] T023 [US2] Unsigned MSIX packaging gate for src/RAGGit.Client.WinUI/RAGGit.Client.WinUI.csproj on Windows (depends on T022) — FRESH artifact at src/RAGGit.Client.WinUI/AppPackages/...x64.msix with AppRuntime 2.x deps. NOTE: 2.x emits to project-root AppPackages/, NOT bin/.../AppPackages/ → CI upload path must be updated (folded into T025–T027)
- [x] T024 [US2] Manual WinUI smoke checklist (sign in → library → upload → ask with citations → history → pagination → admin) on Windows (depends on T023) — PARTIAL: raw-exe launch crashes identically (0xE0434352, DDLM REGDB_E_CLASSNOTREG) on net8/1.5 and net10/2.4.0 builds → environmental (no deployment/test-signing on box), boot parity confirmed, NOT a regression. Full click-through deferred to operator with signed MSIX; no signtool on box

**Checkpoint**: US1 + US2 both work — upgraded server + installable, regression-free desktop client

---

## Phase 5: User Story 3 - CI and delivery stay green on both runners (Priority: P2)

**Goal**: Both CI jobs reference .NET 10 and pass without special casing

**Independent Test**: Push branch, observe Linux + Windows jobs green including MSIX artifact upload (quickstart.md: CI evidence)

### Implementation for User Story 3

- [x] T025 [P] [US3] Update Linux job setup-dotnet 8.0.x → 10.0.x in .github/workflows/ci.yml
- [x] T026 [P] [US3] Update Windows job setup-dotnet 8.0.x → 10.0.x in .github/workflows/ci.yml — plus required fixes: MSIX upload path → project-root AppPackages/ (2.x layout), stale bin-path comment corrected
- [ ] T027 [US3] Push branch and verify both CI jobs green with MSIX artifact upload (depends on T025, T026) — PUSHED as 014-net10-upgrade (3 commits); CI-green verification is an OPERATOR STEP (no gh CLI/token on this box): watch Actions tab for Linux + Windows jobs + MSIX artifact

**Checkpoint**: All user stories independently functional and CI-enforced

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Consistency proof and follow-ups spanning all stories

- [x] T028 Grep all `*.csproj` for stale `8.0.x` PackageReferences and align to the R-04/R-05 policy per specs/014-net10-upgrade/research.md — FOUND 5 explicit net8.0 TFMs overriding Directory.Build.props (fixed → inherit) + runtime-coupled 8.0.x packages (JwtBearer, Data.Sqlite, Caching.Memory, Options → 10.0.12); Serilog/MVVM kept per policy; full Server.slnf + RAGGit.sln re-validated green after
- [x] T029 Run SC-004 consistency audit over pins, projects, Dockerfile, and ci.yml per specs/014-net10-upgrade/data-model.md — all 7 surfaces on new runtime, zero stale pins
- [x] T030 [P] Run quickstart.md validation steps 1–6 end-to-end per specs/014-net10-upgrade/quickstart.md — steps 1–4 + 6 done; step 5 (docker) blocked: no daemon on box
- [ ] T031 File constitution-amendment follow-up as a tracked issue (old-runtime wording → .NET 10) per specs/014-net10-upgrade/spec.md — DRAFT written (specs/014-net10-upgrade/followups/); filing is an OPERATOR STEP (no gh/token): paste into new issue, delete draft. Also notes NU1903 + docker/smoke deferrals

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational completion
  - US1 (P1) and US2 (P1) may proceed in parallel once Foundation is done (different files; US2 needs a Windows runner)
  - US3 (P2) after US1/US2 code changes exist to validate
- **Polish (Phase 6)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: After Foundational — no dependencies on other stories
- **User Story 2 (P1)**: After Foundational — independent of US1 (touches only WinUI project + package refs)
- **User Story 3 (P2)**: After Foundational — validates US1/US2 via CI, should be independently completable

### Within Each User Story

- Pins/versions before builds; builds before test runs; test runs before packaging/smoke
- T017 spike before T018–T021 client changes
- T022 build before T023 packaging before T024 smoke

### Parallel Opportunities

- T002, T003 run in parallel (independent reads)
- T007–T009 run in parallel (different csproj files)
- T025, T026 run in parallel (different YAML blocks, same file — apply sequentially to avoid edit conflicts; marked [P] as order-independent)
- US1 (Linux-capable) and US2 (Windows runner) can proceed in parallel after Foundational
- T030 can run alongside T028–T029 (different surfaces)

---

## Parallel Example: User Story 1

```bash
# Launch independent TFM edits together (different files):
Task: "Update explicit TFM to net10.0 in tests/unit/RAGGit.Tests.Unit.csproj"
Task: "Update explicit TFM to net10.0 in tests/contract/RAGGit.Tests.Contract.csproj"
Task: "Update explicit TFM to net10.0 in tests/integration/RAGGit.Tests.Integration.csproj"

# Sequential validation after edits:
# T011 build → T012 unit/contract → T013 offline → T014 integration → T015 format → T016 docker
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (baselines T001–T003)
2. Complete Phase 2: Foundational (pins T004–T006)
3. Complete Phase 3: User Story 1 (T007–T016)
4. **STOP and VALIDATE**: Server builds, suites green, image builds — MVP shippable for workstation-only deployments
5. Deploy/demo if ready

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add US1 → Test independently → Deploy/Demo (MVP!)
3. Add US2 → Windows build + MSIX + smoke → Deploy/Demo
4. Add US3 → CI green on push → Done
5. Polish → audit + quickstart + amendment follow-up

### Parallel Team Strategy

With multiple developers/runners:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Runner A (Linux): User Story 1
   - Runner B (Windows): User Story 2
3. Either runner: User Story 3 once code changes exist
4. Polish together

---

## Notes

- [P] tasks = different files, no dependencies (T025/T026 share a file — apply sequentially)
- [Story] label maps task to user story for traceability
- Each user story is independently completable and testable
- No new tests written (migration); existing suites are the verification gate per FR-002
- Commit after each task or logical group
- Stop at any checkpoint to validate the story independently
