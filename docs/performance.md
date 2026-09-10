# Performance Tuning — RAGGit Offline-Mode

This document captures the tunable knobs and target success criteria for the
AI Workstation ingestion and query pipeline.

## Ingest (SC-001: 50-page PDF → Ready <5 minutes)

The ingestion pipeline is intentionally chunked into small batches to keep peak
memory reasonable on a 16GB workstation while still meeting the 5-minute target.

| Knob | Default | Purpose |
|------|---------|---------|
| `Ingest:ChunkSize` | 512 | Approximate tokens per chunk |
| `Ingest:ChunkOverlap` | 50 | Overlap between consecutive chunks |
| `Ingest:EmbedBatchSize` | 64 | Max chunks embedded per embedder call |
| `Ingest:EnableChunkCache` | true | Cache extracted chunks by document hash |
| `Ingest:ChunkCacheTtl` | 1 hour | TTL for chunk cache entries |

- A 50-page PDF (~25k tokens) produces roughly 50 chunks.
- At `EmbedBatchSize = 64`, the entire document embeds in a single batch on the
  reference workstation.
- HNSW index parameters are fixed at `m=16` and `efConstruction=128` in
  `LanceDbLocalClient` for a good latency/recall trade-off at 1M chunks.

## Query (SC-002: WAN-off p95 <7 seconds)

| Knob | Default | Purpose |
|------|---------|---------|
| `Cache:Enabled` | true | Enable query embedding cache |
| `Cache:EmbedCap` | 10000 | LRU cap for embedding cache |
| `Cache:EmbedTTLHours` | 24 | TTL for cached embeddings |
| `VectorDb:VectorSize` | 384 | Embedding dimension (384 for all-MiniLM, 768 for nomic) |

- Retrieval target: <2s (LanceDB HNSW search + embedding).
- Generation target: <5s (local LLM chat).
- The health endpoint exposes `p95LatencyMs` so operators can monitor SC-002.

## Benchmark Procedure

Run on the reference workstation with WAN disabled:

```powershell
# SC-001
Measure-Command { dotnet run --project src/RAGGit.Workstation.Api }
# Upload a 50-page PDF via the MAUI client or curl and time until status=Ready.

# SC-002
for ($i = 0; $i -lt 50; $i++) {
    curl -X POST http://ai-workstation.local:5001/api/query `
         -H "X-Api-Key: <employee-key>" `
         -H "Content-Type: application/json" `
         -d '{"query":"refund policy"}'
}
# p95 must be <7000ms.
```

## Known Limits

- Maximum library: 5k documents / ~1M chunks.
- Maximum file size: 100MB.
- Video/audio transcription is out of scope for v1.
