# Quickstart: Remote Workstation Hosting (031)

**Date**: 2026-10-14

Validation guide for running the RAGGit workstation in either deployment mode and pointing the (locally running) WPF desktop client at it. Each scenario maps to a spec success criterion. Configuration keys, ports, and guarantees are defined in [contracts/deployment.md](contracts/deployment.md); durable paths are in [data-model.md](data-model.md). This guide lists runnable steps and expected outcomes, not implementation code.

**Modes** (see [contracts/deployment.md](contracts/deployment.md) §0): **local** (default — workstation on the company network, desktops on the LAN) and **private cloud** (workstation on a company-controlled cloud VM reachable only over the company's private network). Run Scenario 1 **or** Scenario 2, then Scenarios 3–7, which apply to both.

## Prerequisites

- A machine on the company's network capable of running the workstation and its models and storage (local mode).
- For private cloud mode: a DigitalOcean account with a token available to the operator (never committed), operator SSH key, and access to the company's private network/VPN.
- The repository checked out locally; the API image builds from `src/RAGGit.Workstation.Api/Dockerfile`.
- Windows desktop PC with the built WPF client (`src/RAGGit.Client.WPF`) and, for the default certificate path, rights to install a root certificate.
- An `ADMIN_API_KEY` and `EMPLOYEE_API_KEY` value chosen for this deployment.

## Scenario 0 — Local regression (unchanged behavior)

Purpose: prove the feature changes no RAG or REST behavior before touching infrastructure.

```powershell
dotnet build RAGGit.Server.slnf -c Release
dotnet test tests/unit/RAGGit.Tests.Unit.csproj -c Release --no-build
dotnet test tests/contract/RAGGit.Tests.Contract.csproj -c Release --no-build
dotnet test tests/integration/RAGGit.Tests.Integration.csproj -c Release --no-build --filter "QueryOfflineTests|OfflineIdentityTests|OfflineHistoryTests|CrossUserIsolationTests"
```

Expected: the same green counts as the current baseline (AGENTS.md records unit 339 / contract 86 / integration 62 with Ollama up); `RequiresOllama` tests may skip when Ollama is absent. No new tests are required because the REST contract and RAG logic are unchanged (see [research.md](research.md) R-11).

## Scenario 1 — Local mode: stand up on the company network

Purpose: FR-001, FR-006, FR-018; SC-001, SC-010.

1. On the on-network machine, start the workstation using the existing root `docker-compose.yml` (or run the API directly) with local `.env` values; ensure native Ollama is running with the configured embed and chat models pulled.
2. Make the required value choices from [contracts/deployment.md](contracts/deployment.md) §2 and create the first admin account with the operator CLI.
3. From a machine on the same network, verify liveness:

```bash
curl --cacert <company-ca-or-exported-cert> https://<WORKSTATION_HOST>/health
```

Expected: HTTP 200 with `vectorDb` and `llm` both `ok`. Record the elapsed time from fresh machine — must be under 60 minutes. No cloud account was required.

## Scenario 2 — Private cloud mode: provision and bootstrap

Purpose: FR-001, FR-005, FR-006, FR-016, FR-018; SC-001, SC-007, SC-010.

1. Provision (via the DigitalOcean console or the `@deployer` agent): a CPU-only virtual machine, a block-storage volume, an address in the company's private network (VPC/VPN), and network access restricted to the company's own networks.
2. Mount the volume (e.g. `/mnt/raggit-data`) and place `deploy/docker-compose.prod.yml`, `deploy/.env` (from `.env.example`), and the certificate PFX. Generate a certificate whose SAN matches the address clients will use (an **IP SAN** for a private IP, or the VPN host name), per R-03.
3. Run `deploy/bootstrap.sh` (installs Docker + native Ollama, brings the stack up, pulls models, creates the first admin account).
4. Verify liveness from a machine on the company's network:

```bash
curl --cacert <company-ca-or-exported-cert> https://<WORKSTATION_HOST>/health
```

5. Verify the boundary: the same request from a machine **outside** the company's networks must fail (timeout/refused), and no AI/model port may be reachable.

Expected: HTTP 200 with `vectorDb` and `llm` both `ok` from inside; no response from outside. Record the elapsed time from fresh machine — must be under 60 minutes.

## Scenario 3 — Connect a desktop client and sign in (both modes)

Purpose: FR-003, FR-004, FR-008, FR-014, FR-015; SC-002, SC-006.

1. On a Windows PC, copy `deploy/appsettings.client.template.json` into the client's output directory as `appsettings.json` with `Workstation:Url = https://<WORKSTATION_HOST>` and the employee key.
2. For the default certificate path, install the company-issued/self-signed certificate into **Trusted Root Certification Authorities** on that PC.
3. Launch the client and sign in with a per-person account created by the operator.

Expected:
- Sign-in succeeds and the signed-in home screen appears.
- No AI model, index, or document store exists on the PC.
- Negative checks: point `Workstation:Url` at an unreachable host → a clear "cannot reach AI workstation" error within 30 seconds, no hang; remove `Workstation:ApiKey` → the `invalid-config` client with a visible error (never a local fallback); edit only `appsettings.json` (no rebuild) → the new address takes effect.

## Scenario 4 — Upload to the workstation

Purpose: FR-009; SC-003 (first half).

1. From the signed-in client, upload one or more supported documents.
2. Watch each document's status in the client.

Expected: each document advances `Uploading → Queued → Indexing → Ready`, visible in the client; an intentionally corrupt/unextractable file settles to `Failed` with a user-safe reason. Confirm the bytes/derived rows exist under the workstation's data directory and nowhere on the PC.

## Scenario 5 — Query the library with citations

Purpose: FR-010, FR-011; SC-004.

1. Ask a question whose answer is in an uploaded document.
2. Ask a question with no matching content.

Expected: the first answer carries `[chunk-GUID]` citations to the workstation's sources; the second returns exactly `no relevant content found`. Optionally confirm on the workstation that no external AI egress occurs during the query (models are local).

## Scenario 6 — Concurrency

Purpose: SC-008.

Expected: concurrent uploads and queries complete with zero failed uploads and zero unanswered queries, with no concurrency limit enforced (the demo target is a single user; `Ingest:MaxConcurrentJobs` = 2 bounds ingestion). The check is that simultaneous use does not break.

## Scenario 7 — Snapshot, restore, and teardown

Purpose: FR-013; SC-005, SC-009.

1. Snapshot the durable data (private cloud mode: the volume; local mode: the data directory).
2. Rebuild: private cloud mode — destroy the machine, restore the volume to a new machine in the private network; local mode — restore the data directory on a rebuilt machine.
3. Verify sign-in still works and previously uploaded documents are still queryable.
4. Tear down (private cloud mode): snapshot then delete the machine; confirm no billable compute remains and the data copy is retained and restorable.

Expected: after restore, 100% of committed documents and accounts are usable with no re-ingest; teardown leaves nothing billable while the copy survives.

## Failure triage

| Symptom | Likely cause | Check |
|---------|--------------|-------|
| Client shows `invalid-config` | `Workstation:ApiKey` missing (URL alone is not enough) | template config copied correctly |
| `cannot reach AI workstation` | not on the company network/VPN, or wrong host | VPN connected; `curl https://<host>/health` |
| Certificate error on sign-in | certificate not trusted on the PC | install the company/self-signed cert to Trusted Root |
| Request succeeds from outside the company network | firewall/VPC too permissive | tighten 443 to the company's source ranges (R-07) |
| `/health` shows `llm: down` | Ollama not running or model not pulled | `ollama list` on the machine; `OLLAMA_HOST` |
| Startup `DimensionMismatchException` | `VECTOR_SIZE` ≠ embed model | align and re-ingest (wipe `lancedb/`) |
| Upload settles `Failed` | unsupported/empty content or cell cap | `Document.FailureReason` in the client |
