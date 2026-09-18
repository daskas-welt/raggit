# Data Model: Shared Client Core & UI Modernization

**Feature**: `006-client-architecture` | **Date**: 2026-09-14 | **Plan**: [plan.md](./plan.md)

This feature is client-side and introduces **no persistence and no server schema change**. The "entities" are in-memory client types (behavior layer) plus the read-only DTOs reused from `RAGGit.Core` (produced by 004/005). The workstation API contract remains `1.4.0`.

---

## In-memory client entities (`RAGGit.Client.Core`)

### ClientSession

The signed-in person's runtime context and the single source of truth for the UI (FR-001, FR-006/FR-007).

| Field | Type | Rules |
|---|---|---|
| `WorkstationUrl` | `string` | Required for a valid session; absolute `http(s)` URL (validated by `ClientConfigResolver`). |
| `ApiKey` | `string` | Bootstrap credential; required for a valid session. |
| `IdentityType` | `string` | Defaults to `"ApiKey"`. |
| `Role` | `string` | Raises change notification for `Role`/`IsAdmin`. |
| `IsAdmin` | `bool` (derived) | `Role == "Admin"` (case-insensitive). Drives Admin destination visibility (FR-007). |
| `IsValid` | `bool` (derived) | Non-empty `WorkstationUrl` **and** `ApiKey`. |

Behavior: implements `INotifyPropertyChanged`; `Role` change notifies `IsAdmin` too.

### ChatMessage

One entry in the Ask conversation (FR-004).

| Field | Type | Rules |
|---|---|---|
| `Text` | `string` | Message body (default empty). |
| `IsUser` | `bool` | `true` for the person's message; assistant otherwise. |
| `IsAssistant` | `bool` (derived) | `!IsUser` — drives bubble alignment. |
| `Timestamp` | `DateTime` | Defaults to now. |

Relationship: an ordered `ObservableCollection<ChatMessage>` is owned by `QueryViewModel` and rendered by `Components/ChatView`; the assistant message carries the citations for its answer (FR-005). At least the most recent 20 are retained in order (SC-006).

### ClientConfigResult

Outcome of resolving workstation configuration at startup (`ClientConfigResolver`).

| Field | Type | Rules |
|---|---|---|
| `IsValid` | `bool` | `true` only when URL and key are present and the URL is well-formed `http(s)`. |
| `WorkstationUrl` | `string?` | Null when invalid. |
| `ApiKey` | `string?` | Null when invalid. |
| `Error` | `string?` | Human-readable reason (e.g. `Missing Workstation:Url`). |
| `HttpAttempted` | `bool` | Always `false` at resolution — no unauthenticated calls (FR-011). |

Validation rules: missing/blank URL → invalid; missing key (from `Workstation:ApiKey`, `Api:AdminKey`, or `Api:EmployeeKey`) → invalid; non-absolute or non-`http(s)` URL → invalid.

### Status indicator (view concern)

`Components/StatusChip` renders `RAGGit.Core.Models.DocumentStatus`:

| Status | Presentation |
|---|---|
| `Ready` | positive label ("ready"). |
| `Indexing` | in-progress label. |
| `Failed` | error label. |

Rule: identical appearance/labels on every screen that lists documents (FR-008). `DocumentsMineItem.Status` is the string form of this enum.

---

## Reused read-only DTOs (`RAGGit.Core.Models`, from 004/005)

No changes; consumed by `Client.Core` service clients.

| DTO | Fields | Source endpoint |
|---|---|---|
| `HistoryPage` | `Items: List<HistoryItem>`, `Total`, `Limit`, `Offset` | `GET /api/queries/history` |
| `HistoryItem` | `Id`, `PromptPreview`, `AnswerPreview`, `CitationCount`, `LatencyMs`, `CreatedAt` | `GET /api/queries/history` |
| `QueryDetail` | `Id`, `Prompt`, `Answer`, `Citations: List<HistoryCitation>`, `LatencyMs`, `CreatedAt` | `GET /api/queries/{id}` |
| `HistoryCitation` | `DocumentId`, `ChunkId`, `Text`, `Ordinal` | `GET /api/queries/{id}` |
| `DocumentsMinePage` | `Items: List<DocumentMineItem>`, `Total`, `Limit`, `Offset` | `GET /api/documents/mine` |
| `DocumentMineItem` | `Id`, `Filename`, `Size`, `Status`, `CreatedAt` | `GET /api/documents/mine` |
| `Document` | `Id`, `Filename`, `Size`, `Status`, `CreatedBy`, `CreatedAt` | `GET /api/documents` |
| `Query` response | prompt/answer/citations/latency | `POST /api/queries` |

Pagination invariants (unchanged from 005): `limit` default 20 / max 100, `offset` default 0, stable `CreatedAt DESC, Id DESC`; previews truncated at 120 chars + ellipsis.

---

## Service interfaces (behavior layer public surface)

| Type | Responsibility | Backing |
|---|---|---|
| `AuthApiClient` | login / refresh / me | `HttpClient` + `BearerDelegatingHandler` |
| `QueryApiClient` | `POST /api/queries` | `HttpClient` + bearer |
| `QueryHistoryApiClient` | history page + query detail | `HttpClient` + bearer |
| `DocumentsApiClient` | library list, upload, mine | `HttpClient` + bearer |
| `UsersApiClient` | admin user management | `HttpClient` + bearer |
| `SessionTokenStore` | hold/refresh session token | `ISecureStorage` |
| `BearerDelegatingHandler` | attach bearer token | token store |
| `ApiKeyDelegatingHandler` | attach bootstrap API key | session key |
| `IFilePicker` / `ISecureStorage` | platform seams | MAUI adapters / test doubles |

Constraint: no type here performs embedding, vector search, or model calls (FR-012).

---

## State transitions (client session)

```text
            config invalid
startup ─────────────────────▶ ConfigErrorState (fail fast, no HTTP)
   │
   │ config valid
   ▼
signed-out ──login──▶ signed-in (Role set) ──token expiry/401──▶ signed-out
   │                          │
   │                          └── Admin role ⇒ Admin destination visible
   └── non-admin: Admin destination hidden (always)
```

Rules: missing config/service wiring at startup → fail fast with a clear error (FR-011, edge case); expired/deactivated session → next interaction refused and person returned to sign-in (edge case); non-admin requesting the Admin route → refused, destination hidden (FR-007).
