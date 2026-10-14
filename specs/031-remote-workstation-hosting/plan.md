# Implementation Plan: Remote Workstation Hosting

**Branch**: `031-remote-workstation-hosting` | **Date**: 2026-10-14 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/031-remote-workstation-hosting/spec.md` (clarified decisions: public HTTPS endpoint that is firewall-restricted; client configuration delivered via the shipped configuration file; the hosted workstation starts with a fresh empty library).

## Summary

Stand the RAGGit AI Workstation up on a DigitalOcean droplet and repoint the WPF desktop client at it, with no change to RAG behavior. The workstation (ASP.NET Core 10 REST API, Ollama models, LanceDB vectors, SQLite metadata, document content) already runs as a Linux container via `docker-compose.yml` + `src/RAGGit.Workstation.Api/Dockerfile`; this feature turns that development compose into a production deployment: HTTPS termination in-container, state on a durable volume, a locked-down cloud firewall, repeatable provisioning, a client configuration template, and an operator runbook. Almost all of the deliverable is deployment artifacts (`deploy/`, `docs/`) plus documentation of deployment-interface contracts; the application code is already capable (configurable `Workstation:Url` + credential in `ClientConfigResolver`, anonymous `GET /health`, HTTPS redirection, per-person accounts).

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0`, SDK pinned `10.0.401` via `global.json`); container base images `mcr.microsoft.com/dotnet/aspnet:10.0` + `sdk:10.0`.

**Primary Dependencies**: ASP.NET Core (Kestrel + controllers + ApiKey/JWT auth), `LanceDB` local path, `Ollama` (`snowflake-arctic-embed2` 1024-dim + `qwen2.5:3b`), Serilog, Docker Compose on the host, DigitalOcean cloud firewall + block-storage volume + reserved IP. Client: WPF-UI desktop app (unchanged) reading `Workstation:Url` / `Workstation:ApiKey` from `appsettings.json`.

**Storage**: On the droplet, all state under `/app/data` (bind-mounted from a host path on a mounted block-storage volume): `lancedb/` (vectors + `parentId` payloads), `rag.db` (SQLite: documents, chunks, users, query history), `documents/` (original upload bytes), `auth.key` (JWT signing key). Durability comes from the volume + snapshots, not the droplet lifecycle.

**Testing**: Existing suites unchanged (unit, contract, WAN-disabled offline, remaining integration; `RequiresOllama` skips honored) — the REST contract does not change. Deployment acceptance is a new end-to-end runbook check (provision → bootstrap → `GET /health` → client sign-in → upload → query → snapshot/restore → teardown). Any source change is TDD'd per Constitution VI.

**Target Platform**: Linux droplet (Ubuntu LTS) running Docker + native Ollama; Windows desktop clients (existing WPF app) reaching it over HTTPS/tcp 443.

**Project Type**: Deployment/ops feature over the existing server + client (no new project, no new library).

**Performance Goals**: A few concurrent employees (upload + query at once) without failed uploads or unanswered queries (SC-008); first successful employee sign-in within 60 minutes of starting the documented procedure (SC-001); unreachable/untrusted-cert errors surface within 30 seconds (SC-006).

**Constraints**: Only the client-facing HTTPS port is public (SC-007); Ollama (`11434`) and internal storage MUST NOT be publicly reachable (FR-016); the container runs non-root in the stock `aspnet` image and therefore serves HTTPS on `5001` with the host publishing `443:5001`; a password-protected PFX is the certificate secret (the app user cannot read `0600` private keys); no per-deployment source edits (FR-006); single-tenant (FR-017).

**Scale/Scope**: One droplet per company, one workstation instance; ~5 deployment artifacts (`deploy/docker-compose.prod.yml`, `deploy/.env.example`, `deploy/bootstrap.sh`, a client `appsettings` template, root `.dockerignore`) plus one runbook and one deployment-interface contract. No DB schema change, no API change.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Verdict | Notes |
|-----------|---------|-------|
| I. Single-Tenant On-Prem | ⚠️ PASS WITH AMENDMENT | One dedicated droplet per company keeps the single-tenant model (no `company_id`, singleton Library). The "on-prem" and "over LAN/VPN" wording conflicts with a publicly reachable cloud endpoint; the owner approved public HTTPS, so the principle needs a MINOR amendment to permit a company-owned/dedicated host reachable over a secured public endpoint (or VPN). No data-model change. |
| II. Workstation-Owned AI | ✅ PASS | AI (Ollama + LanceDB + API) stays entirely on the workstation host; desktops remain thin (FR-001/FR-002). |
| III. Library-First & Client Reuse | ✅ PASS | No new project/library; client keeps reusing `RAGGit.Client.Core`; no duplicated RAG logic. |
| IV. Offline Invariant (NON-NEGOTIABLE) | ⚠️ PASS WITH AMENDMENT | The invariant's substance holds: the workstation answers queries with its own local models, and the WAN-disabled CI gate still runs against the same server. The wording "desktop query via LAN" assumes a LAN transport; the transport is now the public internet. Amendment clarifies that the invariant constrains the workstation's AI (no query-time AI cloud egress), not the client↔workstation transport. No egress change; suites re-run as proof. |
| V. Citation-Grounded RAG | ✅ PASS | Retrieval/generation untouched; answers remain `[chunk-GUID]`-cited or exactly "no relevant content found". |
| VI. Test-First (NON-NEGOTIABLE) | ✅ PASS | REST contract and RAG logic unchanged, so existing gates are the safety net; any source edit (e.g. `.dockerignore`, prod config) is covered by existing tests, and deployment acceptance is the end-to-end runbook check. |
| VII. Simplicity & Proprietary Stewardship | ✅ PASS | No 4th project; artifacts stay signed/private; deployment artifacts live under `deploy/` + `docs/` per the deployer agent. |
| Technology & Deployment Constraints | ⚠️ PASS WITH AMENDMENT | Stack text still names .NET 8 / Qdrant-era wording and LAN-only networking. Storage is already LanceDB; the amendment (with Principle I/IV above) records the hosted-cloud topology and current runtime. |

**Gate result: PASS** — no Complexity Tracking needed (no extra project, no vector-DB swap, no per-deployment code change). Two governance follow-ups are required and tracked as tasks: a MINOR constitution amendment (`1.3.0 → 1.4.0`) covering hosted-cloud topology (Principles I, IV, Tech & Deployment Constraints), and keeping `.specify/feature.json` pointed at this feature.

*Post-Phase 1 re-check*: `research.md`, `data-model.md`, `contracts/`, and `quickstart.md` introduce no API, schema, or application-code change — only deployment artifacts and interface documentation — so the gate outcome is unchanged: **PASS** (amendments still required as above).

## Project Structure

### Documentation (this feature)

```text
specs/031-remote-workstation-hosting/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (decisions R-01..R-10)
├── data-model.md        # Phase 1 output (deployment entities + persisted data volume)
├── quickstart.md        # Phase 1 output (end-to-end validation runbook)
├── contracts/           # Phase 1 output (deployment interface + "no API change" note)
│   ├── README.md
│   └── deployment.md
├── checklists/
│   └── requirements.md  # Spec quality checklist (all items pass)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
deploy/                                       # NEW — deployment artifacts (deployer allowlist)
├── docker-compose.prod.yml                   # NEW — prod stack: https, volume bind, host.docker.internal
├── .env.example                              # NEW — required env vars (no secrets)
├── bootstrap.sh                              # NEW — install Docker/Ollama, mount volume, place files, up, pull models
└── appsettings.client.template.json          # NEW — per-company client config template (Url + ApiKey)

docs/
└── runbooks/
    └── hosted-workstation.md                 # NEW — provision, verify, snapshot/restore, teardown

.dockerignore                                 # NEW — trim the build context (Dockerfile uses COPY . .)

src/RAGGit.Workstation.Api/
├── Dockerfile                                # reuse; prod compose overrides URLs/ports/cert via env
└── appsettings.json                          # reuse; prod values injected via environment

src/RAGGit.Client.WPF/
└── appsettings.json                          # reuse; per-company URL+key shipped in the template

docker-compose.yml                            # existing dev compose; left as-is (LAN note retained)

tests/                                        # unchanged (unit/, contract/, integration/)
```

**Structure Decision**: No new project or directory under `src/`. The feature adds a `deploy/` artifact set and a `docs/runbooks/` runbook (both already permitted to the `@deployer` agent), reuses the existing Dockerfile and composes instead of forking the server, and reuses the client's existing configuration mechanism instead of adding UI. Rationale: the application already supports a configurable HTTPS workstation URL and a local-model REST API; only the operational packaging and client configuration were missing.

## Complexity Tracking

> No Constitution Check violations require justification. No new project, no vector-DB swap, no new abstraction, and no per-deployment source change. The two ⚠️ verdicts above are documentation/wording amendments handled as follow-up tasks, not complexity exceptions.
