# Implementation Plan: .NET 10 Upgrade

**Branch**: `014-net10-upgrade` | **Date**: 2026-09-18 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/014-net10-upgrade/spec.md` (decisions: now; Windows App SDK 2.x latest stable with fixes in scope; entire repo at once).

## Summary

Retarget the whole repo from .NET 8 to .NET 10 in one workstream: SDK pin → shared build props → all libraries/server/tests → WinUI client (TFM + WindowsAppSDK `2.4.0` + Extensions `10.0.12`) → Dockerfile → CI. No data, contract, or UX change; validation is the existing gates (build, unit/contract/offline/integration, MSIX packaging) plus the manual WinUI smoke checklist. See [research.md](research.md) for pinned versions and rationale.

## Technical Context

**Language/Version**: C# (SDK default for .NET 10 → C# 14, no `LangVersion` pin) — SDK pinned via `global.json` to `10.0.401`, `rollForward: latestFeature`.

**Primary Dependencies**: `Microsoft.WindowsAppSDK 2.4.0` (re-check for newer proven 2.x at implementation); `Microsoft.Extensions.{Configuration.Json,Configuration.UserSecrets,Http} 10.0.12`; `CommunityToolkit.Mvvm` kept at `8.2.2` (fallback `8.4.2` on build failure); `OllamaSharp` kept at `5.4.30` (latest).

**Storage**: N/A (no store changes — SQLite/doc-metadata, LanceDB/Qdrant paths and formats unchanged; see [data-model.md](data-model.md)).

**Testing**: xUnit suites via `dotnet test` — unit (228 baseline), contract, WAN-disabled offline (query/identity/history/isolation), remaining integration; `RequiresOllama` skips honored. Manual WinUI smoke checklist for SC-003.

**Target Platform**: Linux containers + Windows 10 1809+ / 11 desktop (MSIX sideload, x64 packaging gate; AnyCPU remains unsupported for SelfContained — unchanged).

**Project Type**: Runtime/targeting migration across existing server + libraries + WinUI desktop client (no new projects).

**Performance Goals**: No change; no perf gate beyond existing suites (the spec's performance-gate integration test must stay green).

**Constraints**: Zero user-visible behavior change; minimum OS stays 1809; warnings MUST NOT increase vs. baseline (`TreatWarningsAsErrors=false`); portable RIDs stay (`win-x64;win-x86;win-arm64`).

**Scale/Scope**: 9 producible projects + 3 test projects + Dockerfile + CI (2 jobs); single workstream on branch `014-net10-upgrade`.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Verdict | Notes |
|-----------|---------|-------|
| I. Single-Tenant On-Prem | ✅ PASS | No data-model or API change. |
| II. Workstation-Owned AI | ✅ PASS | No AI-ownership change; desktops stay thin. |
| III. Library-First & Client Reuse | ✅ PASS | No new projects, no duplicated logic. Wording references old runtime — follow-up amendment after landing (spec Assumption). |
| IV. Offline Invariant (NON-NEGOTIABLE) | ✅ PASS | No egress change; WAN-disabled suites re-run as the proof. |
| V. Citation-Grounded RAG | ✅ PASS | Retrieval/generation untouched. |
| VI. Test-First (NON-NEGOTIABLE) | ✅ PASS | Pure retargeting: existing suites are the safety net and all gates run; no new behavior to TDD. |
| VII. Simplicity & Stewardship | ✅ PASS | No 4th project; MSIX sideload + versioning unchanged. |
| Tech & Deployment Constraints | ⚠️ PASS WITH FOLLOW-UP | Stack text names the old runtime; amendment (wording) lands after this spec. No deployment-topology change. |

**Gate result: PASS** — no unjustified violations. Complexity Tracking: none required (no violations to justify).

*Post-Phase 1 re-check*: design artifacts (`research.md`, `data-model.md`, `contracts/`, `quickstart.md`) introduce no behavior, data, or structural change — gate outcome unchanged: **PASS**.

## Project Structure

### Documentation (this feature)

```text
specs/014-net10-upgrade/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (versions pinned, R-01..R-09 + spike)
├── data-model.md        # Phase 1 output (no data change + SC-004 audit list)
├── quickstart.md        # Phase 1 output (validation guide, steps 1-6)
├── contracts/           # Phase 1 output (README: no contract changes)
│   └── README.md
├── checklists/
│   └── requirements.md  # Spec quality checklist (16/16)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
# Repo layout touched by this migration (no new directories)
global.json                                  # SDK pin → 10.0.401
Directory.Build.props                        # default TFM → net10.0
src/
├── RAGGit.Core/                             # inherits TFM (verify no override)
├── RAGGit.Ingest/
├── RAGGit.Retrieval/
├── RAGGit.Client.Core/                      # + CommunityToolkit.Mvvm (keep/fallback)
├── RAGGit.Client.WinUI/                     # TFM → net10.0-windows10.0.17763.0,
│                                            # WindowsAppSDK → 2.4.0, Extensions → 10.0.12
└── RAGGit.Workstation.Api/                  # + Dockerfile → :10.0 images
tests/
├── unit/                                    # explicit TFM → net10.0
├── contract/                                # explicit TFM → net10.0
└── integration/                             # explicit TFM → net10.0
.github/workflows/ci.yml                     # both setup-dotnet → 10.0.x
```

**Structure Decision**: In-place retargeting of the existing layout; no new projects, no moved files. Implementation order for `/speckit.tasks`: pins → libraries/server → tests → WinUI client (with 2.4.0 spike validation) → Dockerfile → CI → audit + smoke.

## Complexity Tracking

> No Constitution Check violations to justify — section intentionally empty.
