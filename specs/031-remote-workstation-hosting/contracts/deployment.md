# Deployment Contract: Remote Workstation Hosting (031)

**Date**: 2026-10-14

The interface an operator must satisfy to run the workstation in either mode and point desktops at it. Any `deploy/` artifact, the runbook, and the Compose stack MUST conform to this document. It is not an API contract (the REST API is unchanged — see [README.md](README.md)).

## 0. Deployment modes

Two modes, one codebase, selected by configuration only (FR-018). Employee desktops always run locally and call the workstation's REST API over the network.

| | **Local mode (default)** | **Private cloud mode** |
|---|---|---|
| Workstation runs on | A machine on the company's network | A company-controlled cloud virtual machine |
| Desktops reach it via | the LAN | the company's tailnet (a WireGuard-based VPN overlay; Tailscale is the reference implementation) |
| Deployment artifacts | `deploy/docker-compose.prod.yml` + `deploy/bootstrap.sh` | same artifacts |
| Cloud account | not required | required |
| State location | a local disk path bound at `/app/data` | a block-storage volume bind-mounted at `/app/data` |
| Public exposure | none | **none — zero inbound ports** |

**Both modes use the same artifacts**; the mode is selected purely by `deploy/.env` values (`WORKSTATION_HOST`, `BIND_ADDRESS`, `RAGGIT_DATA_DIR`, and the firewall rules). This satisfies FR-018/SC-010 literally: no source change and no artifact difference between modes. The repository-root `docker-compose.yml` is the *developer* compose — it serves plain HTTP on 5001 and cannot be used with the signed-in client, which requires HTTPS.

## 1. Network surface

**Deployed design: no public inbound ports at all.** The workstation is reachable only over the company's tailnet. The cloud firewall therefore carries **zero inbound rules** — all inbound is denied by default — and the only way in is to be a member of the tailnet.

| Port | Reachable from | Purpose |
|------|----------------|---------|
| 443/tcp (host) → 5001 (container) | tailnet members only | Client HTTPS to the workstation API |
| 22/tcp | tailnet members only (no public rule) | Administration (SSH) |
| 11434/tcp | never | Ollama (host-local, reached via `host.docker.internal`) |
| all others | denied | — |

Because `BIND_ADDRESS=0.0.0.0` publishes `443` on every interface, **the cloud firewall is the only thing keeping it private** — it MUST have no inbound rules. Binding to a single interface is equally valid, provided the published port matches the address clients use.

**Local mode**: the same client-facing service is reachable on the company LAN only; no inbound internet access is required or permitted.

**Guarantees**
- The workstation is never an open public endpoint in either mode (FR-005).
- Only the client-facing HTTPS port is reachable, and only over the company's own network (FR-016, SC-007).
- Ollama and internal storage are never published by Compose.
- Clients MUST use the certificate's name — a **DNS SAN** (the tailnet/MagicDNS name), never the raw tailnet IP: a request to the bare `100.x` address fails TLS validation.

## 2. Required host inputs (`deploy/.env`)

`deploy/.env` holds the operator-facing variables. `deploy/docker-compose.prod.yml` maps each one to the literal container environment variable, which is the .NET nested-config form (`__` = `:`).

| `.env` variable | Container env var | Config key | Required | Example |
|-----------------|-------------------|------------|----------|---------|
| `WORKSTATION_HOST` | (used by clients and for the certificate SAN) | — | yes | `raggit-workstation.<tailnet>.ts.net` (tailnet name) or a LAN name |
| `BIND_ADDRESS` | (compose publishes `${BIND_ADDRESS}:443:5001`) | — | yes | `0.0.0.0` (firewall is the boundary) or the tailnet/LAN IP |
| `RAGGIT_DATA_DIR` | (bind source) | — | yes | `/mnt/raggit-data` |
| `ADMIN_API_KEY` | `Api__AdminKey` | `Api:AdminKey` | yes | (secret) |
| `EMPLOYEE_API_KEY` | `Api__EmployeeKey` | `Api:EmployeeKey` | yes | (secret) |
| `CERT_PFX_PATH` | `ASPNETCORE_Kestrel__Certificates__Default__Path` | Kestrel default cert path | yes | `/mnt/raggit-data/certs/raggit.pfx` |
| `CERT_PFX_PASSWORD` | `ASPNETCORE_Kestrel__Certificates__Default__Password` | Kestrel default cert password | yes | (secret) |
| `EMBED_MODEL` | `Ollama__EmbedModel` | `Ollama:EmbedModel` | no (default `snowflake-arctic-embed2`) | |
| `CHAT_MODEL` | `Ollama__ChatModel` | `Ollama:ChatModel` | no (default `qwen2.5:3b`) | |
| `VECTOR_SIZE` | `VectorDb__VectorSize` | `VectorDb:VectorSize` | no (default `1024`) | |
| `OLLAMA_URL` | `Ollama__Url` | `Ollama:Url` | no (default `http://host.docker.internal:11434`) | |

**Guarantees**
- `.env` is gitignored; the repository ships only `.env.example` (no secrets in the repo, docs, or logs).
- `VECTOR_SIZE` and `EMBED_MODEL` must agree (384/768/1024 ↔ model family) or startup warns/fails per existing validation.
- A bare `OLLAMA_URL` is not a valid .NET key; it MUST be mapped to `Ollama__Url` by the Compose file.
- The Compose file fails fast (`:?`) when a required variable is missing, so the stack cannot start with empty API keys or without a bind address. `BIND_ADDRESS=0.0.0.0` is permitted **only** together with a firewall that denies all inbound (§1).
- `docker compose config` MUST render successfully against `.env.example` (static validation, T027).
- `deploy/bootstrap.sh` also reads `ADMIN_USERNAME`, `ADMIN_DISPLAY_NAME`, and `ADMIN_PASSWORD` to create the first admin account (password passed to the operator CLI over stdin, never in argv).
- Storage keys (`VectorDb__Path`, and the defaulted `ConnectionStrings:RagDb` / `Auth:JwtSigningKeyPath`) resolve under `/app/data` because the API working directory is `/app`; they are covered by the mounted-volume contract in §4.

## 3. Container contract

| Aspect | Value |
|--------|-------|
| Image build | existing `src/RAGGit.Workstation.Api/Dockerfile` (base `aspnet:10.0`, non-root) |
| Listen | `ASPNETCORE_URLS=https://0.0.0.0:5001` |
| Publish | `443:5001` on `BIND_ADDRESS` (`0.0.0.0` is acceptable only behind a deny-all-inbound firewall — see §1) |
| Volume | `RAGGIT_DATA_DIR` bind-mounted at `/app/data` (private cloud mode) |
| Extra host | `host.docker.internal:host-gateway` (Ollama reachability) |
| Restart | `unless-stopped` |
| Health | `GET https://<WORKSTATION_HOST>/health` returns `vectorDb` and `llm` status |

**Guarantees**
- No per-deployment source edit is required (FR-006): all variation is environment.
- The stack is idempotent to bring up: `docker compose -f deploy/docker-compose.prod.yml up -d`.
- The same image and image build serve both modes (FR-018).

## 4. Durable state contract

All of the following MUST survive a machine rebuild (private cloud mode: under `RAGGIT_DATA_DIR` on the volume; local mode: the machine's data directory):

- `lancedb/` — vectors and chunk payloads
- `rag.db` — documents, chunks, accounts, query history
- `documents/` — original uploaded content
- `auth.key` — JWT signing key
- `certs/` — the TLS material (`raggit.pfx` and the distributable `raggit.crt`)

**Guarantees**: rebuilding or restoring the machine from a snapshot preserves 100% of committed documents and accounts (FR-007, SC-005). Backup = volume snapshot; teardown = snapshot then delete compute (R-06, SC-009).

## 5. Client configuration contract

Delivered as `deploy/appsettings.client.template.json`; copied to the deployed client's `appsettings.json` (the client runs locally in both modes).

| Key | Required | Value |
|-----|----------|-------|
| `Workstation:Url` | yes | `https://<WORKSTATION_HOST>` — the tailnet (MagicDNS) name in private cloud mode, a LAN name in local mode |
| `Workstation:ApiKey` | yes | the employee (or admin) key |
| `Workstation:UploadConcurrency` | no | default `4` |

**Guarantees**
- A missing/malformed URL or missing key yields the existing `invalid-config` client with a visible error — never a silent local/hard-coded fallback (FR-004).
- Editing these values requires no rebuild or reinstall (FR-015).
- Sign-in requires `https` (existing client rule).
- The URL MUST match the certificate's SAN. With the default generated certificate that is the tailnet DNS name; the raw tailnet IP will fail TLS validation.
- TLS trust is installed per PC: the generated `raggit.crt` into Trusted Root Authorities, or nothing extra if a publicly trusted certificate is used.

## 6. Bootstrap acceptance (runbook contract)

The documented procedure, run once against a fresh machine in the chosen mode, MUST achieve in order:

1. Local mode: the workstation stack is running on the on-network machine. Private cloud mode: Docker + native Ollama installed, the volume mounted, `deploy/` files and `.env` placed on the VM, and the tailnet joined with **no inbound firewall rules**.
2. Models `EMBED_MODEL` and `CHAT_MODEL` pulled (private cloud mode).
3. `GET https://<WORKSTATION_HOST>/health` returns `ok` for vector store and LLM, from a machine on the company's network (the tailnet).
4. The workstation is confirmed **not** reachable from a machine outside the tailnet — `443`, `22` and `11434` all fail (private cloud mode).
5. First admin account created via the operator CLI (inside the container in private cloud mode).
6. A desktop client with the template config signs in, uploads a document to `Ready`, and gets a cited answer.
7. Total elapsed time from fresh machine to first successful sign-in < 60 minutes (SC-001), in either mode.
