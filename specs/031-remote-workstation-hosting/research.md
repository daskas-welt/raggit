# Research: Remote Workstation Hosting (031)

**Date**: 2026-10-14

All Technical Context values are resolved; no `NEEDS CLARIFICATION` remains. Decisions below are grounded in the repository's existing server/Docker setup and its `@deployer` agent's documented deployment facts (`.opencode/agents/deployer.md`).

## R-01 — Hosting topology: one droplet, Compose stack, native Ollama

**Decision**: One DigitalOcean droplet per company runs the workstation as a single `docker compose` stack (the API container) with Ollama installed natively on the host; a reserved IP is assigned to the droplet and a block-storage volume holds all application data.

**Rationale**: One company = one workstation (Constitution I, FR-017), so a single droplet is the simplest topology that satisfies the spec. The repo already ships a working single-service `docker-compose.yml` and `src/RAGGit.Workstation.Api/Dockerfile`, so the production stack is a hardened variant, not a rewrite. A reserved IP keeps the client-facing address stable across droplet rebuilds (FR-007, SC-005). Ollama runs natively because the existing container reaches it via `host.docker.internal` (already configured), which also keeps GPU/model management on the host.

**Alternatives considered**: (a) Multi-container stack with Ollama in a container — rejected: no benefit here, and it complicates GPU/model provisioning. (b) Kubernetes/managed container hosting — rejected as over-engineering for a single-tenant single-instance service (Constitution VII).

## R-02 — TLS termination in the container (non-root, port 5001)

**Decision**: Terminate TLS inside the API container using Kestrel with a mounted PFX: `ASPNETCORE_URLS=https://0.0.0.0:5001`, `ASPNETCORE_Kestrel__Certificates__Default__Path`, and `ASPNETCORE_Kestrel__Certificates__Default__Password` from `.env`; publish `443:5001` on the host so clients use a bare `https://<host>`.

**Rationale**: The official `aspnet` image runs as a non-root user and cannot bind ports below 1024, so the container serves on 5001 and Docker maps host 443 → container 5001 (documented in the deployer facts). Terminating in Kestrel avoids adding a reverse proxy and keeps `UseHttpsRedirection`, the HTTPS client requirement (`LoginViewModel` rejects non-HTTPS sign-in), and the existing anonymous `GET /health` unchanged.

**Alternatives considered**: (a) A host reverse proxy (nginx/Caddy) — rejected: extra component and cert plumbing for no current benefit. (b) Plain HTTP behind the firewall — rejected: server-to-client credentials would traverse the public network in cleartext (FR-005).

## R-03 — Certificate source: self-signed PFX with IP SAN by default, Let's Encrypt when a domain exists

**Decision**: Default to a self-signed certificate carrying an **IP SAN** for the reserved IP, exported as a password-protected PFX and distributed to client PCs for trust. If the company supplies a domain name, prefer Let's Encrypt and skip the per-PC trust step.

**Rationale**: No domain was provided in the spec's clarifications, so the deployment must work with only an IP. A PFX is the pragmatic secret because the non-root app user cannot read a `0600` `privkey.pem` (deployer fact). Bundling both paths keeps FR-005 satisfiable without blocking on DNS.

**Alternatives considered**: (a) Require a domain up front — rejected: adds an external dependency the operator may not control. (b) Plain HTTP — rejected (FR-005). (c) Client-side certificate-pinning bypass — rejected: weakens validation and is client code the spec does not want.

## R-04 — Client configuration: shipped `appsettings.json` template (no new UI)

**Decision**: Ship a `deploy/appsettings.client.template.json` containing `Workstation:Url` (the `https://<reserved-ip-or-domain>`) and `Workstation:ApiKey`; operators copy it over the client's deployed `appsettings.json` next to the executable. No client code or settings screen is added.

**Rationale**: `ClientConfigResolver` already validates a configurable absolute `https` URL plus `Workstation:ApiKey`/`Api:AdminKey`/`Api:EmployeeKey`, and the WPF app loads `appsettings.json` from `AppContext.BaseDirectory` with `reloadOnChange: false` (no rebuild needed to change it, satisfying FR-015). The configuration-file path matches the clarified scope and the existing `AGENTS.md` "invalid config" behavior (FR-004). A known gotcha applies: a config with `Workstation:Url` but no key resolves to the `invalid-config` client, so the template MUST include a key.

**Alternatives considered**: (a) In-app settings screen — rejected by clarification (scope). (b) Hard-code the host in the binary — rejected: violates FR-004 and `ClientConfigTests.HasHardcodedUrlOrKey`.

## R-05 — Durable state: block-storage volume bind-mounted into the container

**Decision**: Attach a DigitalOcean volume, mount it on the host (e.g. `/mnt/raggit-data`), and bind-mount it to `/app/data` in the container. Everything the workstation persists lives there: `lancedb/`, `rag.db`, `documents/`, `auth.key`.

**Rationale**: The API already writes all state under the `./data`-relative paths derived from `VectorDb:Path`, the SQLite connection string, the JWT key path, and the document content directory. Put all of them on the volume and the droplet becomes disposable — recreate and reattach, and the library, accounts, and JWT signing key survive (FR-007, SC-005). Keeping `auth.key` on the volume also preserves issued sessions across rebuilds.

**Alternatives considered**: (a) Keep state on the droplet's root disk and snapshot the droplet — acceptable but ties data to droplet lifecycle and complicates volume reuse. (b) Managed database services — rejected: single-tenant simplicity (VII) and the offline/no-WAN posture.

## R-06 — Backup & teardown: volume snapshot, then destroy compute

**Decision**: Back up by snapshotting the volume (and/or the droplet); tear down by snapshotting then deleting the droplet, leaving the volume/volume snapshot intact and the reserved IP retargetable.

**Rationale**: Satisfies FR-013/SC-009 without coupling data to compute. The deployer agent's rule against `power-off` (GPU droplets bill while stopped) makes explicit snapshot-then-delete the correct lifecycle.

**Alternatives considered**: (a) `power-off` to save cost — rejected: still bills and is not a data-safety plan. (b) Restore by rebuilding from an image with baked data — rejected: data must live on the volume, not the image.

## R-07 — Network exposure: cloud firewall allows 443 (and 22) only

**Decision**: Apply a DigitalOcean cloud firewall to the droplet allowing inbound tcp 443 (client HTTPS) and tcp 22 restricted to operator/admin source addresses; deny all other inbound. Ollama stays bound to the host reachable only from the Docker bridge (`host.docker.internal`), never publicly.

**Rationale**: Directly implements FR-016 and SC-007. 443 is the only client-facing port; 22 is required for administration (R-09). Ollama's `11434` must never be published.

**Alternatives considered**: (a) UFW on the host instead of a cloud firewall — permitted as defense in depth but a cloud firewall is the primary, documented control and survives host reinstallation. (b) IP allowlisting of office/VPN ranges — offered in the spec as a future narrowing; the firewall rule shape supports it without client changes.

## R-08 — Sizing: CPU-only general-purpose droplet, GPU/scale-up documented

**Decision**: Baseline on a CPU-only general-purpose droplet sized for `qwen2.5:3b` with modest concurrency (start at 4 vCPU / 8 GB; 8 vCPU / 16 GB when more concurrent users are expected). Document a GPU-droplet upgrade path.

**Rationale**: The dev/stated models (`snowflake-arctic-embed2` 1024-dim + `qwen2.5:3b`) run acceptably CPU-only for a small team; the constitution's GPU recommendation is a preference, not a requirement for this workload. `Ingest:MaxConcurrentJobs` is 2, so throughput is bounded by design. Sizing is a documented, changeable parameter, not baked into code.

**Alternatives considered**: (a) GPU droplet by default — rejected as premature cost. (b) Smaller 2 vCPU droplets — rejected: embedding + chat + SQLite will contend, risking SC-008.

## R-09 — Repeatable provisioning: scripted bootstrap over SSH

**Decision**: A checked-in `deploy/bootstrap.sh` (bash, run over SSH by the operator/deployer) installs Docker and Ollama, mounts the volume, writes `deploy/docker-compose.prod.yml` and `.env` on the host, brings the stack up, pulls `snowflake-arctic-embed2` and `qwen2.5:3b`, and creates the first admin account via the operator CLI (`RAGGit.Workstation.Api.dll user add …`). All inputs come from `.env`/parameters.

**Rationale**: Implements FR-006 ("no source edits per deployment") and the deployer workflow (provision → bootstrap → verify → report). Reusing the API image means the operator CLI runs inside the container, so account creation needs no extra tooling.

**Alternatives considered**: (a) Cloud-init user-data — viable but harder to re-run idempotently after a partial failure; the script can be re-run. (b) Manual step list only — rejected: drifts (FR-006).

## R-10 — Health & verification: existing `GET /health` over HTTPS

**Decision**: Use the existing anonymous `GET /health` (returns `vectorDb` and `llm` status) as the remote liveness check, and verify end-to-end with a real sign-in, an upload reaching `Ready`, and a cited query. Mirror the spec's SC checks as runbook steps.

**Rationale**: `HealthController` already reports exactly the two things that matter (vector store and model health) and is `[AllowAnonymous]`, so no new endpoint is needed (FR-012). This keeps the REST contract unchanged and the contract tests untouched.

**Alternatives considered**: Adding a dedicated readiness endpoint — rejected: redundant with `/health` and would expand the API contract for no user value.

## R-11 — Testing strategy: no new behavior to TDD; deployment acceptance is the runbook

**Decision**: Treat this like the `014-net10-upgrade` workstream: the REST contract and RAG logic do not change, so the existing unit/contract/offline/integration suites are the regression net and must stay green. Any incidental source edit (for example adding a root `.dockerignore`, which does not affect runtime behavior) carries no new observable behavior; the deployment-specific acceptance evidence is the `quickstart.md` end-to-end runbook, not a new automated suite.

**Rationale**: Constitution VI mandates TDD for new behavior; there is no new RAG/API behavior here — the deliverables are deployment artifacts and docs. The deployer agent will also collect live verification evidence.

**Alternatives considered**: A mocked "deployment test" suite — rejected: it would test mocks of infrastructure rather than the real deployment, adding maintenance without proof. Adding a container smoke test to CI — deferred; the CI filter (`RAGGit.Server.slnf`) and its `main`/`001-*`…`005-*`/`010-*` branch triggers do not include this branch today (noted as a follow-up, not a blocker).
