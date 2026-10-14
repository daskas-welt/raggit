# Implementation Plan: Remote Workstation Hosting

**Branch**: `031-remote-workstation-hosting` | **Date**: 2026-10-14 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/031-remote-workstation-hosting/spec.md` (clarified decisions: public HTTPS endpoint that is firewall-restricted; client configuration delivered via the shipped configuration file; the hosted workstation starts with a fresh empty library).

## Summary

Support the RAGGit AI Workstation in **two deployment modes** with no change to RAG behavior, and repoint the WPF desktop client (which always runs locally) at whichever mode is chosen:

- **Local mode (default)** - the workstation runs on a machine on the company's own network; desktops call it over the LAN. This is today's mode and needs no cloud account.
- **Private cloud mode** - the workstation runs on a company-controlled cloud virtual machine reachable only over the company's private network (VPN and/or restricted source addresses); it is never an open public endpoint. Desktops stay local and send their API calls to it.

The workstation (ASP.NET Core 10 REST API, Ollama models, LanceDB vectors, SQLite metadata, document content) already runs as a Linux container via `docker-compose.yml` + `src/RAGGit.Workstation.Api/Dockerfile`. The feature adds one TLS-enabled production stack (`deploy/docker-compose.prod.yml`), a bootstrap script, and a single runbook, and **both modes use those same artifacts**, differing only by `deploy/.env` (bind address, data path, firewall, client URL). The repository-root `docker-compose.yml` remains the developer compose and is not used for a signed-in deployment, because it serves plain HTTP and the client refuses non-HTTPS sign-in. Almost all of the deliverable is deployment artifacts (`deploy/`, `docs/`) and deployment-interface documentation; the application code is already capable (configurable `Workstation:Url` + credential in `ClientConfigResolver`, anonymous `GET /health`, HTTPS redirection, per-person accounts), so mode selection is configuration only (FR-018).

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0`, SDK pinned `10.0.401` via `global.json`); container base images `mcr.microsoft.com/dotnet/aspnet:10.0` + `sdk:10.0`.

**Primary Dependencies**: ASP.NET Core (Kestrel + controllers + ApiKey/JWT auth), `LanceDB` local path, `Ollama` (`snowflake-arctic-embed2` 1024-dim + `qwen2.5:3b`), Serilog, Docker Compose on the host. Private cloud mode adds DigitalOcean cloud firewall + block-storage volume + private networking (VPC/VPN) + reserved IP. Client: WPF-UI desktop app (unchanged) reading `Workstation:Url` / `Workstation:ApiKey` from `appsettings.json`.

**Storage**: All state under the workstation's data directory (`lancedb/` vectors + `parentId` payloads, `rag.db` SQLite documents/chunks/users/query history, `documents/` original upload bytes, `auth.key` JWT signing key). Local mode keeps it on the on-network machine's disk; private cloud mode bind-mounts it from the host path on a mounted block-storage volume. Durability comes from the volume + snapshots, not the machine lifecycle.

**Testing**: Existing suites unchanged (unit, contract, WAN-disabled offline, remaining integration; `RequiresOllama` skips honored) — the REST contract does not change. Deployment acceptance is the end-to-end runbook check (`provision → bootstrap → GET /health → client sign-in → upload → query → snapshot/restore → teardown`) run in whichever mode is deployed. Any source change is TDD'd per Constitution VI.

**Target Platform**: Two modes from one codebase — (a) local mode: a machine on the company network running Docker + native Ollama and/or the API directly (Linux or Windows host); (b) private cloud mode: a Linux cloud droplet (Ubuntu LTS) running Docker + native Ollama. Windows desktop clients (existing WPF app) stay local in both modes and reach the workstation over the LAN (local) or the company's private network/VPN (private cloud).

**Project Type**: Deployment/ops feature over the existing server + client (no new project, no new library).

**Performance Goals**: A few concurrent employees (upload + query at once) without failed uploads or unanswered queries (SC-008); first successful employee sign-in within 60 minutes of starting the documented procedure (SC-001); unreachable/untrusted-cert errors surface within 30 seconds (SC-006).

**Constraints**: Only the workstation's client-facing service may be reachable, and only from the company's own networks (SC-007, FR-016); Ollama (`11434`) and internal storage MUST NOT be reachable from outside those networks; the container runs non-root in the stock `aspnet` image and therefore serves HTTPS on `5001` with the host publishing `443:5001` on `BIND_ADDRESS` (`0.0.0.0` only behind a deny-all-inbound firewall) and reachability provided by the company's tailnet rather than any public port; a password-protected PFX is the certificate secret (the app user cannot read `0600` private keys); no per-deployment source edits and no source changes between modes (FR-006, FR-018); single-tenant in both modes (FR-017).

**Scale/Scope**: One workstation instance per company, in exactly one mode at a time; ~6 deployment artifacts (`deploy/docker-compose.prod.yml`, `deploy/.env.example`, `deploy/bootstrap.sh`, a client `appsettings` template, root `.dockerignore`, and the shared runbook) covering both modes; the same artifacts serve both modes, with no per-mode file. No DB schema change, no API change.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Verdict | Notes |
|-----------|---------|-------|
| I. Single-Tenant On-Prem | ✅ PASS (minor wording amendment) | The two modes both stay inside the principle's intent: one dedicated machine per company, on the company's own network (local) or on a company-controlled cloud VM reachable only over the company's private network/VPN (private cloud). Single-tenant in both (no `company_id`, singleton Library). The principle's "on-prem" phrasing is narrowly amended to "on-prem **or company-controlled private cloud**" so the wording matches; no data-model change, no network-boundary change to LAN/VPN. |
| II. Workstation-Owned AI | ✅ PASS | AI (Ollama + LanceDB + API) stays entirely on the company-controlled workstation in both modes; desktops remain thin (FR-001/FR-002). |
| III. Library-First & Client Reuse | ✅ PASS | No new project/library; client keeps reusing `RAGGit.Client.Core`; no duplicated RAG logic. |
| IV. Offline Invariant (NON-NEGOTIABLE) | ✅ PASS (minor wording amendment) | The invariant holds unchanged: the workstation answers queries with its own local models, and the WAN-disabled CI gate still runs against the same server. In private cloud mode the client↔workstation transport crosses the company's private network/VPN rather than a LAN; the amendment clarifies that the invariant constrains the workstation's **AI** (no query-time AI cloud egress), not the transport medium. No egress change; suites re-run as proof. |
| V. Citation-Grounded RAG | ✅ PASS | Retrieval/generation untouched; answers remain `[chunk-GUID]`-cited or exactly "no relevant content found". |
| VI. Test-First (NON-NEGOTIABLE) | ✅ PASS (exemption recorded) | REST contract and RAG logic unchanged, so existing gates are the safety net. `deploy/docker-compose.prod.yml` gets a static check (`docker compose config`, T027); `deploy/bootstrap.sh` is verified by the runbook steps, an exemption accepted by the project owner rather than an omitted gate (research.md R-11). |
| VII. Simplicity & Proprietary Stewardship | ✅ PASS | No 4th project; artifacts stay signed/private; deployment artifacts live under `deploy/` + `docs/` per the deployer agent. Two modes share one codebase and one artifact set. |
| Technology & Deployment Constraints | ⚠️ PASS WITH AMENDMENT | Stack text still names .NET 8 / Qdrant-era wording and says "Network: LAN/VPN only". Storage is already LanceDB, and the deployment stays LAN/VPN-bounded; the amendment records the company-controlled private-cloud option and current runtime wording. |

**Gate result: PASS** — no Complexity Tracking needed (no extra project, no vector-DB swap, no per-deployment code change, no dilution of a MUST principle). The single governance follow-up (T008) has been **applied** on 2026-10-14: the MINOR constitution amendment `1.3.0 → 1.4.0` widened Principle I's "on-prem" phrasing to "on-prem or company-controlled private cloud" and refreshed the stale runtime/network wording in Technology & Deployment Constraints. `.specify/feature.json` already points at this feature.

*Post-Phase 1 re-check*: `research.md`, `data-model.md`, `contracts/`, and `quickstart.md` introduce no API, schema, or application-code change — only deployment artifacts and interface documentation — so the gate outcome is unchanged: **PASS** (minor wording amendment still pending as above).

## Project Structure

### Documentation (this feature)

```text
specs/031-remote-workstation-hosting/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (decisions R-01..R-11)
├── data-model.md        # Phase 1 output (deployment entities + persisted data volume)
├── quickstart.md        # Phase 1 output (end-to-end validation runbook, both modes)
├── contracts/           # Phase 1 output (deployment interface + "no API change" note)
│   ├── README.md
│   └── deployment.md
├── checklists/
│   └── requirements.md  # Spec quality checklist (all items pass)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
deploy/                                       # NEW — deployment artifacts for BOTH modes (deployer allowlist)
├── docker-compose.prod.yml                   # NEW — TLS stack used by both modes: 443 on BIND_ADDRESS, volume, host.docker.internal
├── .env.example                              # NEW — required env vars (no secrets)
├── bootstrap.sh                              # NEW — install Docker/Ollama, mount volume, place files, up, pull models
└── appsettings.client.template.json          # NEW — per-company client config template (Url + ApiKey)

docs/
└── runbooks/
    └── workstation-deployment.md             # NEW — both modes: provision, verify, snapshot/restore, teardown

.dockerignore                                 # NEW — trim the build context (Dockerfile uses COPY . .)

src/RAGGit.Workstation.Api/
├── Dockerfile                                # reuse; private-cloud compose overrides URLs/ports/cert via env
└── appsettings.json                          # reuse; values injected via environment

src/RAGGit.Client.WPF/
└── appsettings.json                          # reuse; per-company URL+key shipped in the template (both modes)

docker-compose.yml                            # existing DEV compose (plain HTTP on 5001) — not used for a signed-in deployment

tests/                                        # unchanged (unit/, contract/, integration/)
```

**Structure Decision**: No new project or directory under `src/`. The feature adds a `deploy/` artifact set used by **both** modes and a single `docs/runbooks/workstation-deployment.md` (both already permitted to the `@deployer` agent), reuses the existing Dockerfile instead of forking the server, and reuses the client's existing configuration mechanism instead of adding UI. The root `docker-compose.yml` is left untouched as the developer compose. Rationale: the application already supports a configurable workstation URL and a local-model REST API; only the operational packaging, the two-mode documentation, and the client configuration were missing.

## Complexity Tracking

> No Constitution Check violations require justification. No new project, no vector-DB swap, no new abstraction, and no per-deployment source change. The remaining ⚠️ verdict is a documentation/wording amendment handled as follow-up task T008, not a complexity exception.
