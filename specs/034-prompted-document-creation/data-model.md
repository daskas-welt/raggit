# Data Model: Description-Based Document Creation

**Feature**: `034-prompted-document-creation` | **Date**: 2026-11-17

No database schema change. This feature reads existing library documents/chunks and returns an in-memory artifact; it does not create query-history, document-library, or content-store records.

## Wire request (`RAGGit.Core.Models`)

### `DocumentCreationRequest`

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `description` | `string` | yes | Non-whitespace; maximum 10,000 characters |
| `format` | `string` | yes | Case-insensitive parse of exactly `docx`, `xlsx`, `pdf`, `txt` |

The server validates the body independently of the client. Invalid values take the endpoint's ordinary JSON error path rather than framework-specific model-binding output.

## Composition input/output

### `DocumentDraft` (transient shared content)

| Field | Type | Notes |
|-------|------|-------|
| `Description` | `string` | User's original goal; used to derive a suggested title/filename |
| `Body` | `string` | Generated content; source-backed factual statements carry `[chunk-guid]` references |
| `Sources` | `IReadOnlyList<Citation>` | Only citations mapped to retrieved chunks; empty means no library sources were used |

No generated draft is serialized to persistent storage. The creation pipeline rejects a source-backed draft when its cited chunk IDs do not belong to the retrieved set or when required source citations cannot be produced.

### Existing `Citation`

The existing Core citation shape is reused: `DocumentId`, `DocumentName`, `ChunkId`, `Text`, and `Ordinal`. Retrieved source IDs are checked against the retrieval result before being included. Source document names and excerpts are rendered in the source appendix.

## Rendered output (`RAGGit.Documents`)

### Existing `GeneratedDocument`

| Field | Type | Notes |
|-------|------|-------|
| `Format` | `DocumentOutputFormat` | One of the four existing formats |
| `FileName` | `string` | OS-safe suggestion with the matching extension |
| `ContentType` | `string` | Existing MIME type mapping |
| `Bytes` | `byte[]` | In-memory output returned by the API |
| `Title` | `string` | Derived from the description |
| `Sources` | `IReadOnlyList<DocumentSource>` | Numbered library references, empty for prompt-only output |
| `RowCount` | `int` | Used by the existing 5,000-row limit |
| `ByteCount` | `int` | `Bytes.Length`, used by the existing 25 MiB limit |

When `Sources` is empty, each renderer includes a clear “No RAGGit sources were used” notice and avoids an empty source appendix. When sources exist, existing numbered references and source appendix behavior is reused. XLSX uses deterministic record/section shaping; it does not make a second model call.

## Client-only state

### `DocumentCreationViewModel`

| Field | Type | Notes |
|-------|------|-------|
| `Description` | `string` | Current user-entered goal |
| `SelectedFormat` | `DocumentOutputFormat?` | No initial selection; one format must be chosen |
| `IsBusy` | `bool` | Prevents concurrent create/save requests |
| `StatusMessage` | `string?` | User-safe validation, source, or failure status |
| `SavedPath` | `string?` | Current page-session path for the Open action; never persisted |

The ViewModel uses the dedicated `DocumentCreationApiClient` and existing `IFileSaveService`. A cancellation or navigation away cancels the in-flight request; a new login/page session does not restore a previous generated file.

## Request flow

```text
Create Document page
  → POST /api/documents/generate { description, format }
  → DocumentCreationController validates + authorizes Employee/Admin
  → DocumentCreationPipeline retrieves relevant chunks using existing RetrievalService
  → GenerationService creates source-backed content, or prompt-only content if no chunks
  → DocumentGenerator renders DocumentDraft in the requested format
  → binary file response with MIME type + sanitized Content-Disposition
  → DocumentCreationApiClient returns bytes
  → IFileSaveService opens the OS save dialog and writes only to the chosen path
```

The request is not stored as an Ask query. The output is not stored as a `Document`, content-store file, generated-document history row, or conversation message.

## Validation and failure rules

| Rule | Owner | Failure |
|------|-------|---------|
| Description nonblank and ≤10,000 characters | API request validation | `400`; generation does not start |
| Format is one of four supported formats | Shared format parser + API validation | `400`; no substitution |
| Caller is Employee/Admin | API authorization | `401`/`403` |
| Retrieved source citation IDs belong to retrieved results | Composition pipeline | Safe `422`; no ungrounded source-backed file |
| No relevant chunks | Composition pipeline | Prompt-only generation with explicit no-source notice |
| Same-person record isolation | Document generation prompt and retrieval/source tests | Other person's facts are excluded |
| Output ≤25 MiB and workbook rows ≤5,000 | `DocumentGenerator` | `422`; no file returned |
| Local model/workstation unavailable | Controller error mapping | `503`; no file returned |
| File save cancelled or fails | Existing `IFileSaveService` | No partial file; cancellation is not an error |
| Generated output retention | Architecture | No persistent write |

## State transitions

None on the workstation. The client page moves through idle → generating → saving → saved, or back to idle with a safe message after cancellation/failure. This state is transient and page-scoped.
