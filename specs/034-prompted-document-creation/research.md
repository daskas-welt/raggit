# Research: Description-Based Document Creation

**Feature**: `034-prompted-document-creation` | **Date**: 2026-11-17

Research is grounded in the active feature specification, the current constitution, the existing 033 implementation, its decision record, and codebase inspection of Retrieval, Documents, API, and WPF.

## R1 — Keep new creation separate from 033 answer export

- **Decision**: Add a new description-based workflow and endpoint. Do not modify `QueryController`, `QueryPipeline` semantics, query persistence, or 033's saved-answer export behavior.
- **Rationale**: 033 renders an already persisted, cited `QueryDetail` and refuses source-free answers. `QueryController` persists each Ask turn; `QueryPipeline` returns exactly `no relevant content found` without invoking the model when retrieval returns no chunks. 034 explicitly requests new documents from a description, with library use optional. Reusing the Ask path would either persist document requests as query history or weaken the existing refusal/grounding contract.
- **Alternatives considered**:
  - Extend Ask's document-intent detection: rejected because it couples new creation to query/answer history and changes 033 behavior.
  - Re-run a saved query to create a new document: rejected because the new document must come from a fresh user description and may be source-free.
- **Prior-decision boundary**: `.specify/assessments/doc-generation/decision.md` parked from-scratch generation specifically for feature 033. The current 034 request is a separate explicit scope; it does not retroactively broaden or rewrite 033. Adoption evidence remains unmeasured and is a product risk, not a technical unknown.

## R2 — Reuse retrieval, but use a document-specific generation path

- **Decision**: Add `DocumentCreationPipeline` in `RAGGit.Retrieval`. It calls the existing `RetrievalService.RetrieveAsync(description, RetrievalService.DefaultTopK)` and uses only chunks that pass the configured retrieval relevance threshold. It calls a document-purpose method on the existing `GenerationService`/`ILlmClient`, rather than changing `QueryPipeline` or using its question-answer prompt.
- **Rationale**: The workstation already owns local retrieval and generation. `GenerationService.GenerateAsync` is question-answer-specific and its empty-context behavior is a refusal; document creation needs an intentional source-free mode and format-aware content. A single use-case pipeline keeps orchestration, source mapping, and prompt constraints testable without adding a project or duplicating embedding/vector search.
- **Alternatives considered**:
  - Build a second vector-search implementation: rejected; it would duplicate `RetrievalService` and risk different relevance rules.
  - Put retrieval, model prompts, and rendering in the controller: rejected; it makes the API layer own business logic and violates library-first boundaries.
  - Reuse `QueryPipeline` unchanged: rejected; it is answer-specific and treats no matches as a refusal.
- **Source behavior**: Always search using the description. If no relevant chunks are returned, compose from the description alone and mark the result as not library-grounded. If chunks are returned, the prompt treats their text as untrusted reference material, applies the existing same-person constraint, asks for chunk citations, and instructs the model to disclose conflicting or missing values rather than guess. Citation IDs are accepted only when they belong to the retrieved set. A source-backed response that cannot provide a valid citation fails safely instead of being silently downgraded to an uncited library-derived document.
- **Known isolation limit**: Retrieval is not filtered by person identity; same-person protection is currently a model-prompt invariant. A fake vector store can test citation filtering and prompt construction, but is not proof of correct model behavior. Keep the existing mixed-record trust tests and add a real-model/eval case when the integration environment supports it.

## R3 — Share a transient content input between composition and rendering

- **Decision**: Add a neutral `DocumentDraft` in `RAGGit.Core.Models` with the original description, generated body, and validated citations. Generalize the existing renderer input to this draft. Keep `DocumentGenerator.Generate(QueryDetail, format)` as the 033 entry point: it preserves existing eligibility and adapts an eligible persisted answer to the draft path.
- **Rationale**: Existing renderers currently accept only `QueryDetail`, while the retrieval library must not depend on `RAGGit.Documents`. A shared Core model keeps the dependency direction valid, supports zero-source prompt-only output, and lets all four formats keep one rendering implementation. Existing `DocumentFileName`, localized labels, `DocumentTableBuilder`, and 25 MiB / 5,000-row caps remain in `RAGGit.Documents`.
- **Alternatives considered**:
  - Convert a new document into a fake persisted `QueryDetail`: rejected because it misrepresents history data and makes query eligibility rules leak into the new flow.
  - Duplicate four new renderers: rejected because output formats and source appendix logic would drift.
  - Add another project: rejected because existing Core, Retrieval, and Documents seams cover the responsibilities.
- **Spreadsheet behavior**: Reuse and generalize `DocumentTableBuilder`'s deterministic record/section shaping. The document prompt requests repeated `label: value` blocks for record tables; free prose remains a titled section sheet. Fix or guard the existing parser behavior so a mixed record/prose answer is not silently dropped and citation markers cannot create orphan indexes when the draft has no sources. No second LLM extraction call is made.
- **Source-free rendering**: Each format clearly states that no RAGGit sources were used; do not emit an empty/ambiguous Sources section. Keep the existing 033 source appendix unchanged for cited `QueryDetail` exports.
- **Citation safety**: In the new draft path, every GUID citation marker must map to a source in the retrieved set. Reject or remove invalid markers rather than leaking an unknown chunk GUID or presenting it as a source reference.
- **Language behavior**: Reuse the existing Greek/English detection and labels used by Ask/document export. Do not add a second language detector or claim new language support beyond what the workstation currently provides.

## R4 — Add one stateless binary API operation

- **Decision**: Add `POST /api/documents/generate` with `{ "description": "...", "format": "docx|xlsx|pdf|txt" }`, authorized for Employee/Admin. Return file bytes, format MIME type, and sanitized `Content-Disposition` filename. The endpoint does not read or write `Queries`, `Documents`, or content-store records.
- **Rationale**: `POST /api/documents` is already the upload route; the `/generate` subroute is distinct. The request carries the full user intent, while the response returns a ready-to-save binary. This adds one additive 1.8.0 contract operation and does not require a database migration or query ID.
- **Alternatives considered**:
  - Reuse `/api/queries/{id}/documents`: rejected because it requires a persisted owned answer and cannot generate from a freeform description.
  - Persist a generated document or request: rejected by FR-017 and adds retention, ownership, and cleanup lifecycle without user value.
  - Return base64 in JSON: rejected because the existing download pattern returns file bytes and content-disposition directly.
- **Failure mapping**: Invalid/missing/overlong description or unsupported format is `400`; authentication/role failures are `401`/`403`; output exceeding existing caps or source-backed output without valid citations is `422`; local model/workstation unavailability is `503`; unexpected render failure is `500`. All errors are user-safe and no partial file is returned.

## R5 — Reuse client save and role/navigation infrastructure

- **Decision**: Add a **Create Document** WPF navigation page for both Employee and Admin. Add a dedicated `DocumentCreationApiClient` with `CreateAsync(description, format, ct)`, use `IFileSaveService.SaveAsync`/`OpenAsync`, and register a transient `DocumentCreationViewModel` and page using existing DI conventions.
- **Rationale**: Existing document saving already guarantees an OS-chosen path and safe file writing. A separate page keeps the new task distinct from Ask, avoids expanding chat state, and makes output format selection explicit. A dedicated client isolates the model-generation timeout and errors from the quick 033 answer-export request; it can reuse the existing binary `DocumentFile` type.
- **Timeout**: The existing document-export client timeout is 10 minutes, shorter than the workstation's configured 900-second model timeout. Configure the new typed creation client to 16 minutes. Do not lengthen `QueryApiClient` or change the workstation model timeout.
- **Alternatives considered**:
  - Add creation controls to the Ask conversation: rejected because it mixes generated-document tasks with query history and 033's answer-export interactions.
  - Save on the workstation or through the temp-file launcher: rejected because output must exist only at a user-chosen local path.

## R6 — Bound inputs and reuse output safety limits

- **Decision**: Require a non-whitespace description of at most 10,000 characters and one of the four existing format values. Reuse the existing 25 MiB file and 5,000-row limits. Do not add configuration for these fixed limits.
- **Rationale**: A bounded prompt prevents unbounded model input while remaining generous for a user description; existing format and output caps already protect renderers and the desktop.
- **Alternatives considered**:
  - No prompt bound: rejected because a huge request can consume prompt context and workstation resources.
  - New configurable limits: rejected as unnecessary for a fixed first-version workflow.

## R7 — Preserve language and authorization conventions

- **Decision**: Use existing Employee/Admin API role authorization and the existing Greek/English language detection/renderer labels. The source library remains company-wide; it is not filtered by uploader. Same-person record isolation must be enforced by source-aware prompting and tested explicitly.
- **Rationale**: This preserves the current identity and language conventions without introducing a new permission model or language classifier. A user-authored description does not confer access to additional documents.
- **Alternatives considered**:
  - Add per-document ownership filtering: rejected because RAGGit's library is currently a shared, single-tenant company library and source documents are not user-private.
  - Add a language dependency/configuration option: rejected; the existing pipeline already owns Greek/English behavior.

## R8 — Verification

- **Decision**: Test pipeline behavior with fake retrieval/LLM collaborators; test each renderer by opening the resulting file; test the API with the existing `TestApiFactory`; verify source isolation and no persistence; construct the new WPF page with the existing screen-load tests.
- **Rationale**: The trust boundary is not proved by successful serialization alone. Output must open, source references must belong to retrieved documents, no generated content may be retained, and the UI must survive XAML construction and binding validation.
- **Commands**: `dotnet build RAGGit.Server.slnf -c Release`; Release unit and contract test projects; the offline integration subset; on Windows, `dotnet test tests/client/RAGGit.Tests.Client.csproj -c Release`; `dotnet csharpier check .`.
- **Additional risk**: `Ollama:Url` is configurable and existing startup validation does not prove that every deployment points to a local host. This feature adds no cloud fallback or external service; deployment configuration and the offline verification gate must keep the actual model endpoint within the workstation boundary.
- **Generation failure**: When relevant chunks were supplied but the model returns no valid source citation, fail safely; do not use `GenerationOptions.FallbackToTopChunk` for this new mode, since the existing fallback can assign a source the model did not cite. Empty retrieval is the only path that intentionally generates source-free content.

## Open technical questions

None. The API, retrieval policy, prompt-only fallback, renderer boundary, and save lifecycle are decided for planning. Demand/usage evidence and the existing prompt-based same-person isolation limit remain explicit risks.
