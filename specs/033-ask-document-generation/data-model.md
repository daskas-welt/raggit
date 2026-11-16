# Data Model: Ask-View Document Output

**Feature**: `033-ask-document-generation` | **Date**: 2026-11-16

No database schema changes. The feature is a read-only projection of an already-persisted, owned `Query`. All new types are transient (request/response/rendering) or client-only state.

## Wire types (`RAGGit.Core.Models`)

### `DocumentOutputFormat` (enum)

| Member | Wire value | Content type | Extension |
|--------|------------|--------------|-----------|
| `Docx` | `docx` | `application/vnd.openxmlformats-officedocument.wordprocessingml.document` | `.docx` |
| `Xlsx` | `xlsx` | `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` | `.xlsx` |
| `Pdf` | `pdf` | `application/pdf` | `.pdf` |
| `Txt` | `txt` | `text/plain; charset=utf-8` | `.txt` |

Rules:
- Wire values are lowercase (`[JsonStringEnumMemberName]`), matching the existing `QueryStage` convention.
- Unsupported formats are not members; the API returns `400` for an unknown value.

### `DocumentRequest`

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `format` | `string` | yes | Exactly one of `docx`, `xlsx`, `pdf`, `txt`; the controller maps it to `DocumentOutputFormat`. A plain string keeps invalid values on the controller's `{ "error": ... }` path instead of the framework's ProblemDetails shape. |

### `DocumentFormatDetection` (shared detector result)

| Field | Type | Notes |
|-------|------|-------|
| `Format` | `DocumentOutputFormat?` | Set only when exactly one supported format and a document-creation intent were found |
| `UnsupportedFormat` | `string?` | Set when a known unsupported token (PowerPoint/pptx/presentation/image/email) was named; no file is produced |
| `IsAmbiguous` | `bool` | True when more than one distinct supported format was named; no automatic file is produced, but the Save as menu remains |

Rules: no format token → all fields empty/false and no file (FR-007); one supported → `Format`; one unsupported → `UnsupportedFormat` and an InfoBar (FR-024); multiple supported → `IsAmbiguous` and no automatic file.

### `QueryResponse` (additive)

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `queryId` | `Guid?` | no | The persisted `Queries.Id`; present on every new answer, including refusals. Absent on older responses/streams; clients MUST handle absence by disabling save. |

### `QueryStreamEvent` (additive)

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `queryId` | `Guid?` | no | Only on the terminal `answer` event; same value as `QueryResponse.QueryId`. |

Existing fields are unchanged. Unknown fields remain ignorable.

## Rendering types (`RAGGit.Documents`)

### `GeneratedDocument`

| Field | Type | Notes |
|-------|------|-------|
| `Format` | `DocumentOutputFormat` | Requested format |
| `FileName` | `string` | Suggested name, sanitized, includes extension |
| `ContentType` | `string` | From the table above |
| `Bytes` | `byte[]` | Rendered file, held in memory only |
| `Title` | `string` | Derived from the query prompt |
| `Sources` | `IReadOnlyList<DocumentSource>` | Ordered as cited |
| `RowCount` | `int` | Data rows rendered (for the 5 000-row cap) |
| `ByteCount` | `int` | `Bytes.Length`, for the 25 MiB cap and logging |

### `DocumentSource`

| Field | Type | Notes |
|-------|------|-------|
| `Index` | `int` | 1-based number used in the body (`[1]`) |
| `DocumentId` | `Guid` | From `HistoryCitation` |
| `DocumentName` | `string?` | Filename when known; never a raw internal id in the body |
| `ChunkId` | `Guid` | Preserved in the appendix for auditability |
| `Ordinal` | `int` | Chunk position in the source document |
| `Excerpt` | `string` | Citation text, rendered in the sources appendix |

**Title and filename rules** (`DocumentFileName`): remove the leading document-command phrase and the format token from the prompt, trim punctuation, keep Unicode letters (Greek included), collapse whitespace, cap at 80 characters, and fall back to `document` when nothing remains. Append the format's extension. The result is sanitized for the OS and is only a suggestion — the employee can rename it in the save dialog.

### `DocumentSection` / `DocumentRow` (Excel shaping)

- `DocumentSection`: `Title` (string), `Rows` (list of `DocumentRow`), `IsRecordShape` (bool).
- `DocumentRow`: `Cells` (ordered list of `DocumentCell`).
- `DocumentCell`: `Header` (string), `Text` (string), `Kind` (`Text|Number|Date`), `NumberValue`/`DateValue` (nullable), `NumberFormat` (string?), `SourceIndexes` (list of int).
- Record shape: header cells are the union of labels in the section, one row per repeated block; absent labels are empty.
- Section shape: two columns, `Section` and `Detail`; one row per non-empty answer line; section title rows are header rows.
- No cell is synthesized. Empty fields remain empty.

## Request flow

```text
Ask (POST /api/queries or /api/queries/stream)
  → QueryPipeline → QueryOutcome → SaveQueryAsync → QueryId
  → QueryResponse.QueryId / answer event.QueryId

Save (POST /api/queries/{id}/documents, format)
  → GetDetailAsync(sub, id)  [ownership]
  → eligible? (not refusal, citations > 0)
  → DocumentGenerator.Render(QueryDetail, format)
  → GeneratedDocument (bytes, filename, content type) → HTTP file response
  → client SaveFileDialog → employee's chosen path
```

No row is written by the save path. The `Queries` row written by the ask path is unchanged in shape.

## Validation and eligibility

| Rule | Where enforced | Failure |
|------|----------------|---------|
| Valid `format` | API model binding / controller | `400` |
| Caller owns the query | `GetDetailAsync(sub, id)` | `404` (never `403`, to avoid id enumeration) |
| Answer is not exactly `no relevant content found` | Renderer eligibility | `422` `no document available` |
| At least one citation | Renderer eligibility | `422` `no document available` |
| Rendered bytes ≤ 25 MiB and rows ≤ 5 000 | Renderer | `422` `document too large` |
| Filename is OS-valid | `DocumentFileName` | Sanitized, never an error |
| Language follows the prompt | `DocumentLabels` | Greek or English labels |
| No persistence of generated bytes | Controller/renderer | Guaranteed by design; no content-store call |

## State transitions

None. A query is answered, persisted and then optionally rendered any number of times; each render is a pure function of the persisted query and the requested format. Generated files are not entities and have no lifecycle beyond the HTTP response and the employee's saved copy.

## Client-only state

### `ChatMessage` (additive)

`ChatMessage` gains observable state so the Saved/Open affordance updates after a save. It derives from `CommunityToolkit.Mvvm.ComponentModel.ObservableObject` (already a `Client.Core` dependency) or implements `INotifyPropertyChanged` equivalently; `QueryId` and `Citations` are set before the message is inserted into the conversation.

| Field | Type | Notes |
|-------|------|-------|
| `QueryId` | `Guid?` | Set from `QueryResponse.QueryId` (live) or `QueryDetail.Id` (history); null disables save. |
| `SavedPath` | `string?` | Last successful save path for the "Open" action; transient, not persisted; raises change notification. |
| `CanSave` | computed | `QueryId != null`, the text is not exactly `no relevant content found`, and `Citations.Count > 0`. |

`ConversationStore` keeps these fields with the message across page navigation; a new login clears them as today.

### `DocumentSaveRequest` (client)

- `Message` (`ChatMessage`), `Format` (`DocumentOutputFormat`) — the per-message save action's parameter object.
- Not serialized; exists only to bind a message + chosen format to one command.
