# Research: Remote Workstation Hosting (031)

**Date**: 2026-10-14

All Technical Context values are resolved; no `NEEDS CLARIFICATION` remains. Decisions below are grounded in the repository's existing server/Docker setup and its `@deployer` agent's documented deployment facts (`.opencode/agents/deployer.md`).

## R-01 — Two deployment modes from one codebase (local default, private cloud alternative)

**Decision**: Support exactly two modes, selected by configuration with no source change. **Local mode** (default): the workstation runs on a machine on the company's own network, using the same TLS stack (`deploy/docker-compose.prod.yml` + `deploy/bootstrap.sh`) with LAN `.env` values, or a direct API run; no cloud account is involved. **Private cloud mode**: one company-controlled cloud virtual machine per company runs the workstation as a single `docker compose` stack (the API container) with Ollama installed natively on the host, a reserved IP, a block-storage volume for all application data, and access restricted to the company's own networks (VPC/VPN and/or firewall source ranges). In both modes the WPF desktop client runs locally and calls the workstation's REST API.

**Rationale**: One company = one workstation (Constitution I, FR-017), and the company's employees sit behind a LAN or a private network either way, so both modes stay inside the principle's LAN/VPN intent. Local mode is the default because it needs nothing external and is closest to today's behavior. Both modes run the same TLS-enabled stack built from `src/RAGGit.Workstation.Api/Dockerfile`, so there is no fork and no per-mode artifact (FR-018, SC-010). A reserved IP keeps the client-facing address stable across machine rebuilds (FR-007, SC-005). Ollama runs natively because the existing container reaches it via `host.docker.internal` (already configured), which also keeps model management on the host.

**Alternatives considered**: (a) A single cloud-only topology — rejected: it forces a cloud account on companies that prefer to stay on-prem and contradicts the default-on-prem posture. (b) Multi-container stack with Ollama in a container — rejected: no benefit here, and it complicates model provisioning. (c) Kubernetes/managed container hosting — rejected as over-engineering for a single-tenant single-instance service (Constitution VII). (d) Exposing the API on the open public internet — rejected: unnecessary for a private-cloud deployment and inconsistent with the LAN/VPN boundary.

## R-02 — TLS termination in the container (non-root, port 5001)

**Decision**: Private cloud mode terminates TLS inside the API container using Kestrel with a mounted PFX: `ASPNETCORE_URLS=https://0.0.0.0:5001`, `ASPNETCORE_Kestrel__Certificates__Default__Path`/`__Password`, and `__Password` from `.env`; the host publishes `443:5001` **bound to the private interface / restricted to the company's networks** so clients use `https://<host>`. Local mode uses the same in-container TLS with a LAN address.

**Rationale**: The official `aspnet` image runs as a non-root user and cannot bind ports below 1024, so the container serves on 5001 and Docker maps host 443 → container 5001 (documented in the deployer facts). Terminating in Kestrel avoids adding a reverse proxy and keeps `UseHttpsRedirection`, the HTTPS client requirement (`LoginViewModel` rejects non-HTTPS sign-in), and the existing anonymous `GET /health` unchanged. Binding the published port to the private interface rather than `0.0.0.0` is what keeps the workstation off the open internet (FR-005, FR-016).

**Alternatives considered**: (a) A host reverse proxy (nginx/Caddy) — rejected: extra component and cert plumbing for no current benefit. (b) Plain HTTP — rejected: credentials would traverse the network in cleartext (FR-005).

## R-03 — Certificate source: company-issued or self-signed PFX by default, publicly trusted only if a public name exists

**Decision**: Default to a certificate whose subject/SAN matches the address clients use (an **IP SAN** for a reserved IP, or the LAN/VPN host name), exported as a password-protected PFX and distributed to client PCs for trust. Use a publicly trusted certificate (for example Let's Encrypt) only if the workstation has a publicly resolvable name and the deployment deliberately fronts it that way.

**Rationale**: A private-cloud workstation is reached by IP or its private name, not a public DNS name, so a publicly trusted certificate is neither available nor necessary; a company-issued or self-signed certificate is the normal case, and trust is installed once per PC as part of deployment. A PFX is the pragmatic secret because the non-root app user cannot read a `0600` `privkey.pem` (deployer fact). Keeping the Let's Encrypt path documented and optional satisfies FR-005 without blocking on DNS.

**Alternatives considered**: (a) Require a public domain up front — rejected: adds an external dependency the operator may not control and conflicts with the private-cloud posture. (b) Plain HTTP — rejected (FR-005). (c) Client-side certificate-pinning bypass — rejected: weakens validation and is client code the spec does not want.

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

## R-07 — Network boundary: private reachability only (no open public endpoint)

**Decision**: In private cloud mode the workstation is reachable only from the company's own networks. Apply the cloud firewall so inbound tcp 443 is allowed **only** from the company's VPN client range and/or its office egress addresses (and tcp 22 only from operator source addresses), denying all other inbound; bind the published `443` to the private interface rather than `0.0.0.0`. Prefer a DigitalOcean VPC plus a VPN so the droplet has no public service at all. Ollama stays bound to the host and is reachable only from the Docker bridge (`host.docker.internal`), never published. Local mode is bounded by the company LAN.

**Rationale**: Directly implements FR-005, FR-016, and SC-007 without relying on the open internet, and keeps the deployment inside the constitution's LAN/VPN intent (R-01). 443 is the only client-facing port; 22 is required for administration (R-09). Ollama's `11434` must never be published.

**Alternatives considered**: (a) A publicly reachable 443 listener on the open internet — rejected: unnecessary for a private-cloud deployment and outside the LAN/VPN boundary the constitution describes. (b) UFW on the host instead of a cloud firewall — permitted as defense in depth, but the cloud firewall/VPC is the primary documented control and survives host reinstallation. (c) Widening access later — an operator may add source ranges or a VPN without any client change.

## R-08 — Sizing: CPU-only general-purpose droplet, GPU/scale-up documented

**Decision**: Baseline on a CPU-only general-purpose droplet sized for `qwen2.5:3b` with modest concurrency (start at 4 vCPU / 8 GB; 8 vCPU / 16 GB when more concurrent users are expected). Document a GPU-droplet upgrade path.

**Rationale**: The dev/stated models (`snowflake-arctic-embed2` 1024-dim + `qwen2.5:3b`) run acceptably CPU-only for a small team; the constitution's GPU recommendation is a preference, not a requirement for this workload. `Ingest:MaxConcurrentJobs` is 2, so throughput is bounded by design. Sizing is a documented, changeable parameter, not baked into code.

**Alternatives considered**: (a) GPU droplet by default — rejected as premature cost. (b) Smaller 2 vCPU droplets — rejected: embedding + chat + SQLite will contend, risking SC-008.

## R-09 — Repeatable provisioning: scripted bootstrap over SSH

**Decision**: A checked-in `deploy/bootstrap.sh` (bash, run over SSH by the operator/deployer for private cloud mode) installs Docker and Ollama, mounts the volume, writes `deploy/docker-compose.prod.yml` and `.env` on the host, brings the stack up, pulls `snowflake-arctic-embed2` and `qwen2.5:3b`, and creates the first admin account via the operator CLI (`RAGGit.Workstation.Api.dll user add …`). All inputs come from `.env`/parameters. The script is mode-agnostic and also serves local mode on a Linux host; alternatively the operator supplies the same configuration keys directly to `dotnet run` (see the runbook).

**Rationale**: Implements FR-006 ("no source edits per deployment") and the deployer workflow (provision → bootstrap → verify → report). Reusing the API image means the operator CLI runs inside the container, so account creation needs no extra tooling.

**Alternatives considered**: (a) Cloud-init user-data — viable but harder to re-run idempotently after a partial failure; the script can be re-run. (b) Manual step list only — rejected: drifts (FR-006).

## R-10 — Health & verification: existing `GET /health` over HTTPS

**Decision**: Use the existing anonymous `GET /health` (returns `vectorDb` and `llm` status) as the remote liveness check, and verify end-to-end with a real sign-in, an upload reaching `Ready`, and a cited query. Mirror the spec's SC checks as runbook steps.

**Rationale**: `HealthController` already reports exactly the two things that matter (vector store and model health) and is `[AllowAnonymous]`, so no new endpoint is needed (FR-012). This keeps the REST contract unchanged and the contract tests untouched.

**Alternatives considered**: Adding a dedicated readiness endpoint — rejected: redundant with `/health` and would expand the API contract for no user value.

## R-11 — Testing strategy: no new behavior to TDD; deployment acceptance is the runbook

**Decision**: Treat this like the `014-net10-upgrade` workstream: the REST contract and RAG logic do not change, so the existing unit/contract/offline/integration suites are the regression net and must stay green. Any incidental source edit (for example adding a root `.dockerignore`, which does not affect runtime behavior) carries no new observable behavior; the deployment-specific acceptance evidence is the `quickstart.md` end-to-end runbook, not a new automated suite.

**Rationale**: Constitution VI mandates TDD for new behavior; there is no new RAG/API behavior here — the deliverables are deployment artifacts and docs. `deploy/docker-compose.prod.yml` gets a cheap static check (`docker compose config`, T027). `deploy/bootstrap.sh` is the one genuinely new executable surface; **by owner decision it is verified by the runbook steps rather than an automated idempotency test** (the live smoke was dropped as disproportionate for a two-script deployment). This exemption is recorded so a future review does not read it as an oversight. The deployer agent will also collect live verification evidence.

**Alternatives considered**: A mocked "deployment test" suite — rejected: it would test mocks of infrastructure rather than the real deployment, adding maintenance without proof. Adding a container smoke test to CI — deferred; the CI filter (`RAGGit.Server.slnf`) and its `main`/`001-*`…`005-*`/`010-*` branch triggers do not include this branch today (noted as a follow-up, not a blocker). A live `bootstrap.sh` re-run/idempotency smoke — dropped by owner decision (T027 covers compose validity only).
