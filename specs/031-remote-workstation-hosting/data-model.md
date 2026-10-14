# Data Model: Remote Workstation Hosting (031)

**Date**: 2026-10-14

This feature introduces **no new application data model**. It reuses the existing `Document` / `Chunk` / `Query` / `Library` / `User` entities in `src/RAGGit.Core/Models` and their SQLite + LanceDB persistence exactly as-is. What this document models is the **deployment surface**: the two deployment modes, the durable artifacts the workstation owns, the configuration that binds a desktop to it, and the lifecycle of a deployment. Field names are the real configuration keys so the plan and tasks can reference them directly.

## Scope note

- No DB schema change, no migration, no new table or column.
- No REST contract change (see [contracts/README.md](contracts/README.md)).
- The `Document` lifecycle (`Uploading → Queued → Indexing → Ready|Failed`) is unchanged and re-specified only as a reference.

## Entity: Deployment Mode

Exactly one mode per deployment; selected by configuration only, no source change (FR-018).

| Attribute | Local mode (default) | Private cloud mode |
|-----------|----------------------|--------------------|
| Workstation host | a machine on the company's network | a company-controlled cloud virtual machine |
| Client reachability | the LAN | the company's tailnet (WireGuard-based VPN overlay; Tailscale reference) |
| Deployment artifacts | `deploy/docker-compose.prod.yml` + `deploy/bootstrap.sh` (same as private cloud) | `deploy/docker-compose.prod.yml` + `deploy/bootstrap.sh` |
| Cloud account | not required | required |
| Public exposure | none | none |

**Validation rule**: the mode determines the `.env` values (bind address, data path, firewall, client URL) and nothing else; switching modes MUST change configuration only, never source code or artifacts.

## Entity: Workstation

The single deployed instance (one per company).

| Field | Source / key | Notes |
|-------|--------------|-------|
| Client-facing address | `WORKSTATION_HOST` | LAN host (local mode) or private/VPN address (private cloud mode) |
| Listen endpoint | `ASPNETCORE_URLS=https://0.0.0.0:5001` | Non-root container port |
| Published host port | `443:5001` | Bound to the private interface / company networks (FR-016) |
| Vector store | `VectorDb:Path`, `VectorDb:Provider=LanceDB`, `VectorDb:VectorSize=1024` | Path under the data dir; must match embed model |
| Embed model | `Ollama:EmbedModel=snowflake-arctic-embed2` | 1024-dim; aligned with `VectorSize` |
| Chat model | `Ollama:ChatModel=qwen2.5:3b` | Local generation |
| Model host | `Ollama:Url=http://host.docker.internal:11434` | Native Ollama, never published |
| Metadata store | `ConnectionStrings:RagDb=Data Source=<data dir>/rag.db` | SQLite |
| Document content | `<data dir>/documents/` | Uploaded bytes |
| JWT signing key | `Auth:JwtSigningKeyPath=<data dir>/auth.key` | On durable storage so sessions survive rebuilds |
| Certificate | Kestrel default cert path/password | PFX mounted read-only (R-02/R-03) |

**Validation rules (already enforced by existing code; unchanged)**: `VectorDb:VectorSize ∈ {384, 768, 1024}`; embed model must align with vector size or startup logs a mismatch warning and dimensions are guarded at startup; blank or missing API keys are rejected by `ClientConfigResolver` on the client side.

## Entity: Persisted Data

The durable unit that outlives the machine (FR-007, SC-005).

| Path (container) | Contents | Persistence importance |
|------------------|----------|------------------------|
| `<data dir>/lancedb/` | Vectors + chunk payloads (child chunks embedded; parents not) | Rebuild without this = re-ingest everything |
| `<data dir>/rag.db` | Documents, chunks, users/accounts, query history | Accounts and library metadata |
| `<data dir>/documents/` | Original uploaded files | Source of truth for content |
| `<data dir>/auth.key` | JWT signing key | Preserves issued sessions across rebuilds |

**Invariant**: in private cloud mode every path above must resolve under the bind-mounted volume (`RAGGIT_DATA_DIR` → `/app/data`); none may live on the VM's ephemeral/root disk. In local mode they live in the machine's data directory.

## Entity: Client Configuration

The per-desktop settings that point a client at the workstation. Delivered as `deploy/appsettings.client.template.json`; the operator copies it to the deployed client's `appsettings.json`.

| Field | Key | Validation (existing `ClientConfigResolver`) |
|-------|-----|---------------------------------------------|
| Workstation URL | `Workstation:Url` | Required; absolute `http`/`https` URI; sign-in additionally requires `https`; LAN host in local mode, private/VPN host in private cloud mode |
| Credential | `Workstation:ApiKey` (or `Api:AdminKey` / `Api:EmployeeKey`) | Required — a missing key yields the `invalid-config` client |
| Upload concurrency | `Workstation:UploadConcurrency` | Optional (existing; default 4) |

**Rules**: no hard-coded host/key may exist in source (`ClientConfigTests.HasHardcodedUrlOrKey`); changing these values must not require a rebuild or reinstall (FR-015).

## Entity: Deployment Profile

The operator-supplied inputs in `deploy/.env` (see [contracts/deployment.md](contracts/deployment.md) §2 for the full `.env` → container-env → config-key mapping).

| `.env` variable | Config key |
|-----------------|------------|
| `WORKSTATION_HOST` | client `Workstation:Url` host (LAN or private/VPN) |
| `RAGGIT_DATA_DIR` | bind source for `/app/data` |
| `ADMIN_API_KEY` → `Api__AdminKey` | `Api:AdminKey` |
| `EMPLOYEE_API_KEY` → `Api__EmployeeKey` | `Api:EmployeeKey` |
| `CERT_PFX_PATH` / `CERT_PFX_PASSWORD` | Kestrel default certificate |
| `EMBED_MODEL` → `Ollama__EmbedModel` | `Ollama:EmbedModel` |
| `CHAT_MODEL` → `Ollama__ChatModel` | `Ollama:ChatModel` |
| `VECTOR_SIZE` → `VectorDb__VectorSize` | `VectorDb:VectorSize` |
| `OLLAMA_URL` → `Ollama__Url` | `Ollama:Url` |

**Rules**: `.env` is gitignored; only `.env.example` is committed (secrets never surface in the repo, docs, or logs). Nested .NET keys use `__`, not `_`.

## Entity: Deployment Lifecycle

State of the deployment as an operator sees it (identical in both modes; private cloud mode additionally has teardown).

```text
                provision                 bootstrap
  (none) ───────────────► Provisioned ───────────────► Serving
     ▲                          │                          │
     │                          │                          │ snapshot
     │                     destroy (no                     ▼
     │                      data yet)                  Serving+Backup
     │                          │                          │
     └──────────────────────────┴───────────── tear down ◄─┘
                                    (compute gone, volume + snapshot kept)
```

| State | Meaning | Exit conditions |
|-------|---------|-----------------|
| Provisioned | Machine exists (plus volume, address, firewall in private cloud mode); stack not yet up | bootstrap succeeds → Serving |
| Serving | API healthy over HTTPS; sign-in, upload, query work | snapshot → Serving+Backup |
| Serving+Backup | A restorable copy exists; safe to destroy compute | restore → Serving; tear down → TornDown |
| TornDown | No billable compute; data copy retained | restore to new compute → Serving |

**Acceptance linkage**: SC-001 (provision→sign-in < 60 min, either mode), SC-005 (restore preserves data), SC-009 (teardown leaves nothing billable while a copy survives).

## Entity: Data Snapshot

A restorable copy of the durable data (see R-06). Attributes: creation time, source (volume or machine), size, and a restore target. Restoring MUST reproduce 100% of committed documents and accounts (SC-005).

## Referenced (unchanged) entities

`Document`, `Chunk` (child/parent), `Query`, `Library`, `User` — see `src/RAGGit.Core/Models` and the persisted shapes documented in `specs/001-offline-mode/data-model.md`. Their behavior, fields, and lifecycle are untouched by this feature.
