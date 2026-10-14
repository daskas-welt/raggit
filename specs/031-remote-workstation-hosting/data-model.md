# Data Model: Remote Workstation Hosting (031)

**Date**: 2026-10-14

This feature introduces **no new application data model**. It reuses the existing `Document` / `Chunk` / `Query` / `Library` / `User` entities in `src/RAGGit.Core/Models` and their SQLite + LanceDB persistence exactly as-is. What this document models is the **deployment surface**: the durable artifacts the hosted workstation owns, the configuration that binds a desktop to it, and the lifecycle of a hosted deployment. Field names are the real configuration keys so the plan and tasks can reference them directly.

## Scope note

- No DB schema change, no migration, no new table or column.
- No REST contract change (see [contracts/README.md](contracts/README.md)).
- The `Document` lifecycle (`Uploading → Queued → Indexing → Ready|Failed`) is unchanged and re-specified only as a reference.

## Entity: Hosted Workstation

The single deployed instance (one per company).

| Field | Source / key | Notes |
|-------|--------------|-------|
| Public address | reserved IP or domain | Stable across rebuilds; used in `Workstation:Url` |
| Listen endpoint | `ASPNETCORE_URLS=https://0.0.0.0:5001` | Non-root container port |
| Published host port | `443:5001` | Only client-facing public port (FR-016) |
| Vector store | `VectorDb:Path=/app/data/lancedb`, `VectorDb:Provider=LanceDB`, `VectorDb:VectorSize=1024` | Must match embed model (existing validator) |
| Embed model | `Ollama:EmbedModel=snowflake-arctic-embed2` | 1024-dim; aligned with `VectorSize` |
| Chat model | `Ollama:ChatModel=qwen2.5:3b` | Local generation |
| Model host | `Ollama:Url=http://host.docker.internal:11434` | Native Ollama, not publicly exposed |
| Metadata store | `ConnectionStrings:RagDb=Data Source=/app/data/rag.db` | SQLite |
| Document content | `documents/` under the data dir | Uploaded bytes |
| JWT signing key | `Auth:JwtSigningKeyPath=/app/data/auth.key` | On the volume so sessions survive rebuilds |
| Certificate | `ASPNETCORE_Kestrel__Certificates__Default__Path` / `__Password` | PFX mounted read-only (R-02/R-03) |

**Validation rules (already enforced by existing code; unchanged)**: `VectorDb:VectorSize ∈ {384, 768, 1024}`; embed model must align with vector size or startup logs a mismatch warning and dimensions are guarded at startup; blank or missing API keys are rejected by `ClientConfigResolver` on the client side.

## Entity: Persisted Data Volume

The durable unit that outlives the droplet (FR-007, SC-005).

| Path (container) | Contents | Persistence importance |
|------------------|----------|------------------------|
| `/app/data/lancedb/` | Vectors + chunk payloads (child chunks embedded; parents not) | Rebuild without this = re-ingest everything |
| `/app/data/rag.db` | Documents, chunks, users/accounts, query history | Accounts and library metadata |
| `/app/data/documents/` | Original uploaded files | Source of truth for content |
| `/app/data/auth.key` | JWT signing key | Preserves issued sessions across rebuilds |

**Invariant**: every path above must resolve under the mounted volume; none may live on the droplet's ephemeral/root disk.

## Entity: Client Configuration

The per-desktop settings that point a client at the hosted workstation. Delivered as `deploy/appsettings.client.template.json`; the operator copies it to the deployed client's `appsettings.json`.

| Field | Key | Validation (existing `ClientConfigResolver`) |
|-------|-----|---------------------------------------------|
| Workstation URL | `Workstation:Url` | Required; must be an absolute `http`/`https` URI; sign-in additionally requires `https` |
| Credential | `Workstation:ApiKey` (or `Api:AdminKey` / `Api:EmployeeKey`) | Required — a missing key yields the `invalid-config` client |
| Upload concurrency | `Workstation:UploadConcurrency` | Optional (existing; default 4) |

**Rules**: no hard-coded host/key may exist in source (`ClientConfigTests.HasHardcodedUrlOrKey`); changing these values must not require a rebuild or reinstall (FR-015).

## Entity: Deployment Profile

The set of operator-supplied inputs placed in `deploy/.env` (see [contracts/deployment.md](contracts/deployment.md)), mirrored from the compose file.

| Variable | Meaning |
|----------|---------|
| `RAGGIT_PUBLIC_HOST` | Reserved IP or domain clients use |
| `ADMIN_API_KEY`, `EMPLOYEE_API_KEY` | `Api:AdminKey` / `Api:EmployeeKey` |
| `CERT_PFX_PATH`, `CERT_PFX_PASSWORD` | Certificate secret (never committed) |
| `RAGGIT_DATA_DIR` | Host path of the mounted volume (bind source) |
| `OLLAMA_URL`, `EMBED_MODEL`, `CHAT_MODEL` | Overrides for `Ollama:*` |

**Rules**: `.env` is gitignored; only `.env.example` is committed (secrets never surface in the repo, an ADR, or logs).

## Entity: Deployment Lifecycle

State of the hosted deployment as an operator sees it.

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
| Provisioned | Droplet + volume + reserved IP + firewall exist; stack not yet up | bootstrap succeeds → Serving |
| Serving | API healthy over HTTPS; sign-in, upload, query work | snapshot → Serving+Backup |
| Serving+Backup | A volume snapshot exists; safe to destroy compute | restore → Serving; tear down → TornDown |
| TornDown | No billable compute; volume + snapshot retained | reattach + up → Serving |

**Acceptance linkage**: SC-001 (provision→sign-in < 60 min), SC-005 (restore preserves data), SC-009 (teardown leaves nothing billable while a snapshot survives).

## Entity: Data Snapshot

A restorable copy of the volume (see R-06). Attributes: creation time, source volume, size, and a restore target (new volume attached to a rebuilt droplet). Restoring MUST reproduce 100% of committed documents and accounts (SC-005).

## Referenced (unchanged) entities

`Document`, `Chunk` (child/parent), `Query`, `Library`, `User` — see `src/RAGGit.Core/Models` and the persisted shapes documented in `specs/001-offline-mode/data-model.md`. Their behavior, fields, and lifecycle are untouched by this feature.
