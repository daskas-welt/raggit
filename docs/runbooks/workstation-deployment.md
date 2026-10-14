# RAGGit Workstation - Deployment Runbook

Covers both deployment modes for the AI Workstation (feature `031-remote-workstation-hosting`).
The desktop client always runs locally on the employee's PC; only the workstation's location
and reachability differ.

|  | **Local mode** (default) | **Private cloud mode** |
|---|---|---|
| Workstation runs on | a machine on the company's network | a company-controlled cloud virtual machine |
| Client reaches it via | the LAN | the company's private network (VPN and/or restricted source addresses) |
| Cloud account | not required | required |
| Public exposure | none | **none** - never an open public endpoint |
| `BIND_ADDRESS` | the machine's LAN IP | the machine's private/VPN IP |
| `RAGGIT_DATA_DIR` | a local disk path | the mounted block-storage volume |

Both modes use the same artifacts - `deploy/docker-compose.prod.yml`, `deploy/bootstrap.sh`, and a
`deploy/.env` derived from `deploy/.env.example`. **Mode is selected by configuration only**; there
is no source-code difference between them. Configuration keys, ports and guarantees are specified in
[contracts/deployment.md](../../specs/031-remote-workstation-hosting/contracts/deployment.md).

> **HTTPS is mandatory.** The desktop client refuses to sign in over `http://`. The TLS-enabled stack
> is `deploy/docker-compose.prod.yml`. The repository-root `docker-compose.yml` is the *developer*
> compose: it serves plain HTTP on 5001 and **cannot be used with a signed-in client**.

---

## 1. Prerequisites (both modes)

- A machine that can run Docker + the models: 4 vCPU / 8 GB baseline, 8 vCPU / 16 GB for more users;
  more disk if the library grows. **Size the embed model to the host**: on CPU-only hardware the
  1024-dim default embeds slowly enough to make a large library impractical - see section 10.
- The repository checked out on that machine (the scripts and compose live in `<repo>/deploy/`).
- `deploy/.env` filled in from `deploy/.env.example` - at minimum `WORKSTATION_HOST`,
  `BIND_ADDRESS`, `RAGGIT_DATA_DIR`, both API keys, and the certificate path/password.
- Model agreement: `VECTOR_SIZE` and `EMBED_MODEL` must match (384 / 768 / 1024 ↔ model family) or
  startup warns or fails. Defaults: `snowflake-arctic-embed2` + `1024`.
- On each employee PC: the built WPF client and the trust material for the workstation certificate.

### Secrets

`deploy/.env` is gitignored - never commit it. Only `.env.example` is in the repository. A
password-protected PFX is used for TLS; the container runs as a non-root user, so the PFX must be
readable by it (`chmod 644` is sufficient for a password-protected file on a network that is already
private).

---

## 2. Bring-up (automated path, Linux host with `sudo`)

`deploy/bootstrap.sh` is idempotent and works for **both** modes: it installs Docker and native
Ollama, wires Ollama for container access, pulls the models, generates a certificate whose SAN
matches `WORKSTATION_HOST` (if one is not already present), brings the stack up, waits for `/health`,
and creates the first admin account.

```bash
# on the workstation machine, from the repository root
cp deploy/.env.example deploy/.env
$EDITOR deploy/.env                      # set WORKSTATION_HOST + BIND_ADDRESS to this machine
sudo ./deploy/bootstrap.sh
```

Re-running is safe: an existing certificate is reused, an existing admin account is detected
(the operator CLI exits 2 for "already exists"), and the compose stack is reconciled.

### Manual path (no bootstrap script)

```bash
# 1. Ollama + models, reachable from the container
OLLAMA_HOST=0.0.0.0 ollama serve &      # or a systemd drop-in; see bootstrap.sh
ollama pull snowflake-arctic-embed2
ollama pull qwen2.5:3b

# 2. Put the certificate where CERT_PFX_PATH points (a container path under /app/data)
mkdir -p "${RAGGIT_DATA_DIR}/certs" && chmod 644 "${RAGGIT_DATA_DIR}/certs/raggit.pfx"

# 3. Bring the stack up
docker compose -f deploy/docker-compose.prod.yml up -d --build
```

### No-Docker path (single machine, local mode)

The same configuration keys can be supplied as environment variables to a direct run:

```bash
ASPNETCORE_URLS="https://0.0.0.0:5001" \
ASPNETCORE_Kestrel__Certificates__Default__Path="$PFX" \
ASPNETCORE_Kestrel__Certificates__Default__Password="$PFX_PASSWORD" \
Api__AdminKey="$ADMIN_API_KEY" Api__EmployeeKey="$EMPLOYEE_API_KEY" \
dotnet run --project src/RAGGit.Workstation.Api -c Release
```

---

## 3. Certificate

The certificate's subject/SAN must match **the address clients use** (an IP SAN for a private IP,
otherwise a DNS SAN for the VPN/LAN host name). A publicly trusted certificate is only relevant if
the workstation has a publicly resolvable name, which a private-cloud deployment normally does not.

- `bootstrap.sh` generates a self-signed certificate automatically and also writes a PEM copy to
  `${RAGGIT_DATA_DIR}/certs/raggit.crt` for distribution.
- To issue one yourself: `openssl req -x509 -newkey rsa:2048 -sha256 -days 825 -nodes -keyout k.pem
  -out c.pem -subj "/CN=<host>" -addext "subjectAltName=IP:<host>"` then
  `openssl pkcs12 -export -out raggit.pfx -inkey k.pem -in c.pem -passout pass:<password>`.
- Store it at the host path corresponding to `CERT_PFX_PATH` (i.e. `${RAGGIT_DATA_DIR}/certs/…`).

---

## 4. Reachability (private cloud mode)

The workstation must be reachable **only** from the company's own networks, and in the deployed design
it has **no public inbound ports at all** — access is over a tailnet (a WireGuard-based VPN overlay;
Tailscale is the reference implementation).

1. **Join the tailnet.** Install Tailscale on the workstation and on each client PC, and sign in to the
   same tailnet. The workstation needs no auth key: `tailscale up` prints a login URL that the operator
   approves in a browser.
2. **Firewall: leave inbound empty.** The cloud firewall denies all inbound by default — do **not** add
   a rule for `443` or `22`. Administration happens over the tailnet as well. A temporary inbound `22`
   rule from `0.0.0.0/0` is acceptable only during first bring-up (key-only auth, no passwords) to get
   in, and MUST be removed once tailnet SSH is confirmed working from a client PC.
3. **Bind address.** `BIND_ADDRESS=0.0.0.0` publishes `443` on every interface, and the firewall is
   what keeps it private. Binding to a single interface is equally valid, provided the published port
   matches the address clients actually use.
4. **Certificate name.** `WORKSTATION_HOST` must be the name clients use — the tailnet DNS name
   (MagicDNS), e.g. `raggit-workstation.<tailnet>.ts.net` — so the certificate's **DNS SAN** matches.
   Clients MUST use that name, not the raw tailnet IP: a request to the bare `100.x` address fails TLS
   validation.

Verify from a client PC that is on the tailnet:

```bash
curl --cacert raggit.crt https://<WORKSTATION_HOST>/health
```

Then confirm from a machine **outside** the tailnet that `443`, `22` and `11434` are all unreachable.

In local mode the same controls are typically just the LAN itself; nothing needs to be reachable from
outside the LAN.

---

## 5. Verify

```bash
# From a machine on the company network (use the PEM copy or your CA):
curl --cacert raggit.crt https://<WORKSTATION_HOST>/health
```

Expected: HTTP 200 with `vectorDb` and `llm` both `ok`.

**Boundary check (private cloud mode)** - run the same request from a machine *outside* the
company's networks. It must **fail** (timeout or connection refused). If it succeeds, the firewall
is too permissive: tighten `443` to the company's source ranges before going further.

**Admin account** (created by `bootstrap.sh`; the CLI runs against the same volume):

```bash
docker compose -f deploy/docker-compose.prod.yml exec -T raggit-workstation \
  dotnet RAGGit.Workstation.Api.dll user add --username <name> --display-name "<Name>" \
  --role Admin --password-stdin        # password on stdin, never in argv
```

The CLI must run with `/app` as the working directory so the database path resolves inside the
mounted volume; `bootstrap.sh` does this for you. Exit code `2` means the username already exists.

---

## 6. Client configuration (both modes)

On each employee PC:

1. Copy `deploy/appsettings.client.template.json` over the deployed client's `appsettings.json`
   (next to `RAGGit.Client.WPF.exe`).
2. Set `Workstation:Url` to `https://<WORKSTATION_HOST>` - the LAN address in local mode, the
   private/VPN address in private cloud mode.
3. Set `Workstation:ApiKey` to the employee key. **Both values are required**: a URL without a key
   resolves to the `invalid-config` client.
4. Install the workstation certificate (`raggit.crt`) into **Trusted Root Certification Authorities**
   so the HTTPS connection is trusted. Skip this only if the certificate is publicly trusted.

No rebuild or reinstall is needed to change these values - editing the file is enough.

---

## 7. Health & monitoring

- `GET https://<WORKSTATION_HOST>/health` (anonymous) is the liveness check. `vectorDb` and `llm`
  each report `ok` or `down`; anything `down` means queries will fail.
- `llm: down` almost always means Ollama is not running or a model is missing on the host
  (`ollama list`). `vectorDb: down` means the LanceDB path is unreadable - check the volume mount.
- Container logs: `docker compose -f deploy/docker-compose.prod.yml logs -f raggit-workstation`.
- A workstation that cannot be reached at all is a network-boundary problem, not an application one:
  re-check VPN connectivity and the firewall rules in section 4.

---

## 8. Data safety: snapshot, restore, teardown

Everything durable lives under `RAGGIT_DATA_DIR`: `lancedb/`, `rag.db`, `documents/`, `auth.key`,
and `certs/`. Keeping `auth.key` on the volume preserves issued sessions across a rebuild.

**Snapshot** (private cloud mode): snapshot the volume (and/or the machine). Local mode: back up the
data directory.

**Restore / rebuild**:

1. Snapshot the volume/directory.
2. Destroy the machine (private cloud mode) and create a replacement in the same private network.
3. Reattach/restore the volume at `RAGGIT_DATA_DIR`, or restore the directory.
4. Re-run `sudo ./deploy/bootstrap.sh` (or just `docker compose … up -d` if the certificate and
   `.env` are intact).
5. Verify section 5, then confirm an existing account signs in and existing documents are still
   queryable - no re-ingest should be required.

**Teardown** (private cloud mode): snapshot, then **delete the machine**. Do not merely stop it -
stopped compute still bills. The volume/snapshot is retained and can be restored later.

---

## 9. Troubleshooting

| Symptom | Likely cause | Check |
|---|---|---|
| Client shows `invalid-config` | `Workstation:ApiKey` missing (URL alone is not enough) | section 6, step 3 |
| `cannot reach AI workstation` | not on the company network/VPN, wrong host, or port blocked | VPN connected; `curl https://<host>/health` |
| Certificate error on sign-in | certificate not trusted, or its SAN does not match the address used | install `raggit.crt`; re-issue with the right SAN |
| Request succeeds from outside the company network | firewall/VPC too permissive | section 4 |
| `/health` shows `llm: down` | Ollama not running, or model not pulled | `ollama list`, `systemctl status ollama` |
| Stack will not start | `deploy/.env` missing or incomplete | compose fails fast on missing `BIND_ADDRESS`, keys, or cert |
| Startup `DimensionMismatchException` | `VECTOR_SIZE` ≠ `EMBED_MODEL` | align, wipe `lancedb/`, re-ingest |
| Upload settles `Failed` with `corrupted pdf` / `no extractable content` | unsupported or unreadable content (e.g. a scan with no text layer) | `Document.FailureReason` in the client |
| Upload settles `Failed` with `indexing failed; see workstation logs for details` | `EMBED_BATCH_SIZE` too large for the host - each embed request exceeds `Ollama:TimeoutMs` | section 10; set `EMBED_BATCH_SIZE=8` on CPU |

---

## 10. Performance and model sizing

Ingest cost is dominated by **embedding every chunk**, so the embed model and the batch size decide
whether a deployment is practical on a given host. Measure before committing a large library.

| Host | Embed model | `VECTOR_SIZE` | Relative ingest speed | Notes |
|---|---|---|---|---|
| 4 vCPU, CPU-only | `snowflake-arctic-embed2` | 1024 | 1x (~7 s per ~900-token input, measured) | Best retrieval quality; impractical for a large library on CPU |
| 4 vCPU, CPU-only | `all-minilm` | 384 | ~15-25x faster | Pragmatic CPU choice; noticeably weaker retrieval |
| GPU, 20 GB+ | `snowflake-arctic-embed2` | 1024 | orders of magnitude faster | Preferred when a GPU is available |

**`EMBED_BATCH_SIZE` must fit inside `Ollama:TimeoutMs` (default `300000` ms = 5 min).** The app embeds
chunks in batches of `EMBED_BATCH_SIZE`; Ollama processes one input at a time per slot, so a batch of
`N` chunks takes roughly `N x per-chunk time`. The application default (64) is a GPU-sized value: on a
CPU-only host 64 x 7 s is ~7.5 minutes, which exceeds the timeout, so **every batch times out and every
document lands on `Failed`**. The shipped `deploy/.env.example` therefore sets `EMBED_BATCH_SIZE=8`;
raise it toward 64 only on a host that can embed a batch in seconds.

Changing `VECTOR_SIZE` or `EMBED_MODEL` invalidates existing vectors - LanceDB rejects a different
dimension with `DimensionMismatchException`. To switch models on an existing library:

```bash
# 1. stop the stack and drop the old vectors (dimension-specific)
docker compose -f deploy/docker-compose.prod.yml down
rm -rf "${RAGGIT_DATA_DIR}/lancedb"

# 2. re-queue every document so the ingest worker re-processes it on startup.
#    Originals are retained under documents/, so nothing needs re-uploading.
python3 - <<'PY'
import sqlite3, os
db = os.environ["RAGGIT_DATA_DIR"] + "/rag.db"
con = sqlite3.connect(db)
con.execute("UPDATE Documents SET Status = 1, FailureReason = NULL")  # 1 = Queued
con.commit(); con.close()
PY

# 3. edit .env (EMBED_MODEL / VECTOR_SIZE / EMBED_BATCH_SIZE), pull the model, restart
ollama pull all-minilm
docker compose -f deploy/docker-compose.prod.yml up -d
```

The ingest worker re-queues anything left in `Uploading`, `Queued`, or `Indexing` on startup, so step 2
is what makes the whole library re-ingest. Watch progress with
`docker compose -f deploy/docker-compose.prod.yml logs -f raggit-workstation`.

### Keep both models resident

Ollama's default `OLLAMA_MAX_LOADED_MODELS=1` makes the embed and chat models **evict each other**,
so every query reloads the chat model (2.2 GB) from disk between embedding the question and
generating the answer. `deploy/bootstrap.sh` sets `OLLAMA_MAX_LOADED_MODELS=2` and
`OLLAMA_KEEP_ALIVE=-1`. Measured on a 4 vCPU CPU-only host (`qwen2.5:3b` + `all-minilm`,
`mode: auto`): **154 s → 112 s** per query, with both models held in memory (~2.3 GB).

### Where query time actually goes

On CPU-only hardware the dominant cost is **prompt processing of the retrieved context**, not answer
generation — an answer of ~80 tokens still took ~110 s. Levers, cheapest first:

| Lever | Cost | Effect |
|---|---|---|
| Model residency (above) | free | measured 27% |
| Fewer/smaller retrieved chunks | free; retrieval-quality trade-off | roughly proportional to context size |
| A smaller chat model | free; answer-quality trade-off | large |
| More vCPU (CPU-only resize) | money; **reversible on DigitalOcean within 24 h** | ~1.6-2x - measure before committing |
| A GPU | money + provider access | the only order-of-magnitude fix |

Prompt processing parallelises across cores better than token generation does, so a CPU resize helps
more than the generation speed alone would suggest — but it is a linear cost for a sub-linear gain.

---

## 11. Pausing and restoring a private-cloud deployment

**A powered-off Droplet still bills.** DigitalOcean keeps charging until the Droplet is
*destroyed*, because the compute stays reserved on the hypervisor. To stop paying while keeping the
library, snapshot the state and destroy the compute.

### Pause

1. **Copy `deploy/.env` off the machine first.** It is *not* on the volume, and it holds both API
   keys, the certificate password, and the admin password. Losing it means new API keys (every
   client's `appsettings.json` must be updated) and a new certificate.
2. **Snapshot the volume** (this is the backup of `lancedb/`, `rag.db`, `documents/`, `auth.key`,
   `certs/`):
   ```bash
   doctl compute volume snapshot <volume-id> --snapshot-name raggit-data-paused
   ```
3. **Destroy the Droplet** — this is what stops compute billing.
4. **Release the reserved IP.** An unassigned reserved IPv4 costs $5/month. The unassign that
   DigitalOcean runs automatically on destroy can fail (`remove_ip errored` in the action log),
   leaving the IP unreleasable via the API with `422 The reserved IP action is unprocessable`; if
   that happens, release it from the control panel instead.
5. **Delete the volume if you are keeping the snapshot.** The snapshot is $0.06/GiB-month (50 GB =
   $3/month); the volume itself is $0.10/GiB-month (50 GB = $5/month).

Ongoing cost: the snapshot only.

### Restore

1. Create a volume **in the same region** from the snapshot (at least its `min_disk_size`, 50 GB),
   attach it to a new Droplet, and mount it at `RAGGIT_DATA_DIR`.
2. Check out the repository on the new machine and restore `deploy/.env`.
3. `sudo ./deploy/bootstrap.sh` — it installs Docker and Ollama, re-pulls the models, and brings the
   stack up against the restored volume. The certificate already on the volume is reused, so
   `WORKSTATION_HOST` must still match its SAN.
4. Re-join the tailnet and verify (section 5).

**The Tailscale node identity does not survive the rebuild.** Re-enrolling typically yields a new
MagicDNS name (`raggit-workstation-1`), which no longer matches the certificate's DNS SAN — either
rename the node back in the Tailscale admin console or regenerate the certificate and redistribute
`raggit.crt` to every client. Delete the stale node so its name is freed.

Nothing else is lost: the Ollama models, the container image, and the repository checkout are all
reproducible by `bootstrap.sh`. Only `deploy/.env` needs to have been saved by hand.


