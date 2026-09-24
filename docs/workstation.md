# Running the RAGGit Workstation (for operators)

The workstation is the one powerful company machine that does all the AI
work. Employee PCs are thin clients — if the workstation stops, the whole
company stops getting answers. This page covers running it day to day.

## What runs on the box

| Piece | What it is | Typical location |
|---|---|---|
| `RAGGit.Workstation.Api` | The web service employees' apps talk to (HTTPS port 5001) | Installed service or `dotnet run` |
| `ollama serve` | The local AI (embeddings + chat), port 11434 | System service |
| `./data` | The library: vector index + metadata database | Never deleted casually |
| `./models` | Downloaded AI weights (one-time download) | Never redistributed |

Employee PCs need none of this — only the desktop app and the network.

## Daily operation

1. **Start Ollama first**: `ollama serve` (or leave it as a service — preferred).
   Confirm the models are present: `ollama list` (see `EmbedModel` /
   `ChatModel` in `appsettings.json`).
2. **Warm the model**: ask one throwaway question (or `ollama run
   <chat-model>`). A cold model can take minutes to load — the first real
   user query must not be the one that discovers this.
3. **Start the API** (HTTPS, LAN interface):
   `dotnet run --project src/RAGGit.Workstation.Api --urls https://0.0.0.0:5001`
4. **Verify**: `GET https://<host>:5001/health` →
   `200 {vectorDb: ok, llm: ok}`. Then sign in via the desktop app once.

## Configuration

All in `src/RAGGit.Workstation.Api/appsettings.json` (secrets via user
secrets or environment, never in the file):

- `VectorDb: { Path, VectorSize }` — size must match the embedding model
  (384 for `all-minilm`, 768 for `nomic-embed-text`); after a swap, delete
  `./data/lancedb` and re-ingest.
- `Ollama: { Url, EmbedModel, ChatModel, TimeoutMs }` — chat timeout
  defaults to 120s; on slow CPU-only boxes a cold or heavy generation can
  exceed it (the API logs `Query timed out`, the user gets an error, nothing
  hangs). Warm models and matched hardware are the fix — raising the
  timeout only masks slowness.
- People and API keys: [Operator guide](./operator-cli.md).

## Going and staying offline

After the one-time model download, the workstation needs no internet:
disable WAN, keep LAN, and re-run the health check plus one query —
both must succeed. CI enforces this automatically on every change.

## Backups and disk

- Back up `./data` (library + accounts) on a schedule; exclude nothing
  inside it.
- `./models` can be re-downloaded — back it up only if bandwidth is scarce.
- If disk fills up, queries fail: keep 10GB+ free (models + growth).

## Inspecting the vector index (read-only)

Use the Lance data viewer to look inside `./data/lancedb` (table
`library`) without touching the API. It is read-only and runs locally —
pull needs WAN once, running it is LAN-only and offline-safe.

```powershell
docker pull ghcr.io/lance-format/lance-data-viewer:latest
docker run --rm -p 8080:8080 -v "C:\path\to\data\lancedb:/data:ro" ghcr.io/lance-format/lance-data-viewer:latest
# UI: http://localhost:8080 -> library
# Health: Invoke-RestMethod http://localhost:8080/healthz
```

Bash variant:

```bash
docker pull ghcr.io/lance-format/lance-data-viewer:latest
docker run --rm -p 8080:8080 \
  -v /path/to/data/lancedb:/data:ro \
  ghcr.io/lance-format/lance-data-viewer:latest
```

Which folder to mount:

| Environment | Mount this host folder as `/data:ro` |
|---|---|
| Dev (repo checkout) | `src/RAGGit.Workstation.Api/data/lancedb` |
| Prod workstation | `./data/lancedb` (i.e. `VECTORDB__PATH=/app/data/lancedb` in `docker-compose.yml`) |

Rules:

- Always mount `:ro` and mount the folder containing `library.lance`,
  never the `.lance` folder itself.
- Old image name `ghcr.io/lancedb/lance-data-viewer` returns
  `unauthorized` from GHCR — the org moved to `lance-format`.
- If the API is running, the index may be locked — stop the API first
  or copy `data/lancedb` to a temp folder and mount the copy.
- Never write through the viewer: the writer is the .NET `LanceDB`
  SDK (`src/RAGGit.Ingest`), and `VectorDb:VectorSize` (384|768|1024)
  must still match the embedding model.
- `latest` floats; if the viewer ever fails to open the table after a
  re-pull, pin a versioned tag (e.g. `lancedb-0.33.0`) for that inspection.

## Troubleshooting

- **Query times out, health was ok**: model cold or box overloaded — warm
  it, check CPU/RAM, consider a smaller chat model or GPU. See notes above.
- **`503 model unavailable offline`**: Ollama stopped or unreachable —
  restart `ollama serve`, check `Ollama:Url`.
- **Employees get "cannot reach AI workstation"**: LAN/VPN or firewall,
  not RAGGit — verify port 5001 from their subnet.
- **Wrong-vector-size errors at startup**: embedding model and
  `VectorSize` disagree — align them, wipe `./data/lancedb`, re-ingest.

Related: [Publishing](publish.md) · [Operator CLI](operator-cli.md) ·
[Performance](performance.md) · employee side: [Installing the app](install.md).
