# Research: Real Workstation Bring-Up

**Feature**: `002-real-bringup` | **Date**: 2026-09-11 | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

## Summary

Phase 0 resolves 7 decisions for proving the real offline loop (Ollama + LanceDB file-backed, no fakes) and wiring the thin MAUI client, with the dev laptop as measurement source and the reference workstation as production assumption. All decisions stay within constitution v1.1.0 (I single-tenant, II workstation-owned AI, IV offline invariant, VII MINOR bump for additive contract).

## Decisions

### R1 — LanceDB dimension introspection & guard timing

**Decision**: Startup guard inspects existing `library` table schema via `Connection.OpenTable("library")` then reads Arrow `Schema` `vector` field `FixedSizeListType.ListSize` and compares to `VectorDb:VectorSize` (384|768). If table absent, allow lazy creation on first ingest; if mismatch, fail-fast at startup with message `Configured VectorSize {configured} does not match existing collection dimension {stored} — delete data/lancedb or migrate/re-index` (FR-001/SC-004/Q3). First-request check remains as backstop for lazy path.

**Rationale**: `LanceDbLocalClient.EnsureTableAsync` already creates the vector column with `FixedSizeListType(vectorField, _vectorSize)`; inspecting before creation avoids silent junk. Startup normative per Q3.

**Alternatives**: First-request-only — rejected (would delay misconfig until ingest). Auto-migration — rejected (out of scope for this feature; wipe is prescribed).

**Resolved 2026-09-11 (probe `lancedb 2.5.0` + `Apache.Arrow 22.1.0`)**: Verified via live SDK probe:
```csharp
var table = await connection.OpenTable("library");
var schema = await table.Schema(); // or var arrow = await table.ToArrow(); var schema = arrow.Schema;
var vectorField = schema.GetFieldByName("vector");
var stored = ((FixedSizeListType)vectorField.DataType).ListSize; // 384 or 768
```
`ToArrow().Schema` yields same. Fallback via failed upsert not needed. Guard in `LanceDbLocalClient.ValidateDimensionAsync` will use `await table.Schema()` and throw `DimensionMismatchException` with both dims. Probe output: `ListSize=384` confirmed.

### R2 — Configurable embed model (all-minilm 384 dev / nomic-embed-text 768 prod)

**Decision**: `Ollama:EmbedModel` selects the Ollama model; `VectorDb:VectorSize` declares the dimension and must align: `all-minilm` → 384, `nomic-embed-text` → 768. Dev `appsettings.json` defaults `all-minilm` + `VectorSize:384` + `phi3:mini` (8-16GB RAM); prod override `nomic-embed-text` + `768` + `llama3.2:3b`. Config validated at startup together with R1.

**Rationale**: FR-001 + Assumptions (dev laptop vs prod workstation). Dimension mismatch otherwise produces cosine-nearest garbage.

**Alternatives**: Derive VectorSize from embed probe at startup — rejected (requires live Ollama at boot, breaks offline invariant when Ollama down).

### R3 — Guard vs /health precedence

**Decision**: Dimension mismatch is startup failure — process exits before `IHostedService` healthy; `/health` is then unreachable (correct). If mismatch is only detected as backstop (lazy table, first ingest), `/health` returns `vectorDb: down` with detail `dimension mismatch {configured} vs {stored}` and DoesNotStart-style guidance. Legacy `Qdrant:Path` disagreement emits startup `Warning` log but does not fail (single source `VectorDb:Path`).

**Rationale**: Spec S3 requires fail-fast with actionable message; health must not mask misconfig.

**Resolved 2026-09-11**: Shared constant `DimensionMismatchException.MessageTemplate = "Configured VectorSize {configured} does not match existing collection dimension {stored} — delete data/lancedb or re-index/migrate"` used by both startup `DimensionGuardHostedService` and `/health` backstop (`vectorDb: down` with same detail). Probe confirms single message unification.

### R4 — Contract tooling decision

**Decision**: `contracts/api.yaml` remains source of truth for contract tests using `Microsoft.AspNetCore.Mvc.Testing` as in 001. No OpenAPI codegen (NSwag/Kiota) for this feature; add `AuthApiClient` manually alongside `DocumentsApiClient`/`QueryApiClient`. Bump `info.version` to `1.1.0` MINOR (Q2 additive envelope).

**Rationale**: 001 shipped without codegen; introducing it would churn diff for a single `GET /api/auth/me` addition. Keep tooling stable.

**Resolved 2026-09-11**: `/swagger` retained as-is; no codegen package added per R4. Manual `AuthApiClient` suffices for 1.1.0.

### R5 — xUnit opt-in skip mechanics for real-Ollama suite

**Decision**: Real-Ollama tests tagged `[Trait("RequiresOllama","true")]` and gated by a helper `OllamaAvailable` that does `GET {Ollama:Url}/api/tags` with 1-2s timeout. When probe fails, test uses `Assert.Skip` / `Skip.If` pattern or pre-set `[Fact(Skip="Ollama unavailable")]` via conditional fact `SkippableFact` (choose one after probe). CI without Ollama stays green (SC-003); `dotnet test --filter RequiresOllama` runs opt-in.

**Rationale**: FR-002 + SC-003 "skip gracefully" + CI determinism with fakes.

**Resolved 2026-09-11 (probe xunit 2.5.3 in repo)**: `Assert.Skip` / `Xunit.SkippableFact` not available in xunit 2.5.3 without extra package. Decision: no new package; helper `OllamaProbe.IsAvailableAsync(string url, 2s)` does `GET {url}/api/tags` with 1.5s timeout. Opt-in tests carry `[Trait("RequiresOllama","true")]`; test body starts with `if (!await OllamaProbe.IsAvailableAsync(...)) return;` so CI without Ollama passes (graceful skip, SC-003). `dotnet test --filter RequiresOllama` still enumerates them. Verified build probe compiles without new dependency.

### R6 — MAUI config loading (Workstation Url + API key)

**Decision**: Thin client reads `Workstation:Url` and key material (`Workstation:ApiKey` preferred, fallback `Api:AdminKey`/`Api:EmployeeKey`) from `RAGGit.Client.Maui/appsettings.json` (MauiAsset) loaded via `MauiAppBuilder.Configuration.AddJsonFile("appsettings.json")` + `AddUserSecrets` for dev. `MauiProgram.CreateMauiApp` extended with `CreateWithOptions(IConfiguration)` that registers `HttpClient` with `BaseAddress = Workstation:Url` and `X-Api-Key` via `DelegatingHandler`. Missing url/key → launch config error UI, never unauthenticated calls (spec edge case). Flags `--workstation`/`--api-key` are fictional — documented as such in quickstart.

**Rationale**: FR-003 no hard-coded URLs/keys; current `MauiProgram.cs:31` uses `CreateDefault()` with no wiring.

**Resolved 2026-09-11 (probe `RAGGit.Client.Maui.csproj`)**: `MauiAsset` (via `MauiAsset` item + `AddJsonFile("appsettings.json")` through `MauiAppBuilder.Configuration`) required for `net8.0-windows10.0.19041.0` / `-ios` / `-android`; for `net8.0` fallback CI the same file is also registered as `Content PreserveNewest` + `EmbeddedResource` fallback. Verified `MauiAppBuilder.Configuration.AddJsonFile` exists in MAUI host; `net8.0` fallback keeps `net8.0` TFM building without workload. Decision retained.

### R7 — Ollama timeout & offline fail-fast

**Decision**: All Ollama HTTP calls (`EmbedAsync`, `ChatAsync`, probe) use `HttpClient.Timeout = 2-5s` (tunable via `Ollama:TimeoutMs`). On timeout / unreachable / model not pulled → return `503 {error: "model unavailable offline"}` or `503 AI workstation unavailable` at `POST /api/query`, and for client the thin layer maps to `cannot reach AI workstation` / `AI workstation unavailable` within the same timeout. Never `ollama pull` at query time.

**Rationale**: Constitution IV `model unavailable offline` + spec edge case "no hang waiting for cloud pull" + FR-007.

**Resolved 2026-09-11**: Default `Ollama:TimeoutMs = 5000` (5s) balances 8GB laptop `phi3:mini` first-token while keeping offline fail-fast < timeout. Probe: `HttpClient.Timeout = TimeSpan.FromMilliseconds(timeoutMs)` applied to both `OllamaEmbedder` and `OllamaLlmClient`. Unreachable model → 503 within timeout, never `ollama pull`. SC-002 verification will record actual wall time; 5s is the dev default, prod may tune via config.

## Alternatives Considered

- Auto-reindex on dimension swap — deferred (requires embedding every doc again; wipe prescribed as recovery for 002).
- ONNX/LLamaSharp as primary embedder — rejected for acceptance (FR-005); remains behind non-default `Ingest:Embedder=Onnx` flag.
- Mobile on-device LLM — explicitly deferred per Q1 (Windows/localhost is acceptance).

## References

- `src/RAGGit.Ingest/Vector/LanceDbLocalClient.cs:40-108` EnsureTableAsync + FixedSizeListType creation
- `src/RAGGit.Workstation.Api/appsettings.json:6-8` VectorDb/VectorSize + Ollama EmbedModel/ChatModel/Url
- `src/RAGGit.Client.Maui/MauiProgram.cs:31` CreateDefault() — to be extended
- `spec.md Q1-Q3` clarifications (client platforms, auth envelope, guard timing)
- `.specify/memory/constitution.md v1.1.0` principles IV, VII
