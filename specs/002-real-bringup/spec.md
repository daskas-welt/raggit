# Feature Specification: Real Workstation Bring-Up

**Feature Branch**: `002-real-bringup`

**Created**: 2026-09-11

**Status**: Draft

**Input**: User description: "Real workstation bring-up: make the offline RAG loop run for real with Ollama and LanceDB, wire the thin MAUI client, configurable embed model (all-minilm 384 dev / nomic-embed-text 768 prod), measured SC-001/SC-002 on dev hardware; dev is one laptop, prod is a separate workstation box (end users never run the LLM)."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Run the Offline Loop for Real (Priority: P1)

A developer runs the workstation API locally against real Ollama (`all-minilm` 384d on the dev laptop; `phi3:mini` for chat) and real LanceDB (`./data/lancedb`), with no fake vector store or fake embedder/LLM in the path. The developer uploads real PDFs, sees them become searchable, and queries them and gets a cited answer over LAN (or localhost) with WAN simulated-off.

**Why this priority**: `001` proved the contract and the API surface against fakes. The whole product thesis — *offline RAG on LAN works* — has never been measured. Until this loop is proven end-to-end (SC-001/SC-002 measured, not asserted), every downstream capability inherits the same unknown.

**Independent Test**: On a clean checkout, start `Ollama` (`all-minilm` + `phi3:mini`) and `dotnet run --project src/RAGGit.Workstation.Api`. Upload a real 50-page PDF via `curl -F file=@sample.pdf` or the desktop, assert `GET /api/documents` shows `Ready` in <5 min, then `POST /api/query {"query":"refund policy"}` (with and without WAN) returns `200 {answer, citations}` with at least one citation or `no relevant content found`. Measured on the dev laptop; the reference-workstation number is recorded as a production assumption, not faked. Re-run with `nomic-embed-text` (768d) requires a fresh `data/lancedb` (dimension mismatch is expected). Real-Ollama tests are opt-in and skip gracefully when `http://localhost:11434` is absent so CI remains green with fakes.

**Acceptance Scenarios**:

1. **Given** Ollama is running with `all-minilm` (384) and a fresh `data/lancedb`, **When** the API starts, **Then** it starts successfully and `/health` reports both vector DB and LLM as healthy.
2. **Given** Ollama with `all-minilm`, **When** an admin uploads a real 50-page PDF (<100MB), **Then** the document reaches `Ready` in <5 min on the dev laptop, appears in `GET /api/documents`, and a query for text from that document returns it among the top-5 with a citation.
3. **Given** Ollama with `nomic-embed-text` (768) against a DB created with 384, **When** the API starts, **Then** it fails fast with an actionable message that the configured dimension (768) does not match the existing collection and names the re-index path (wipe `data/lancedb` or migrate), rather than returning silent wrong results.
4. **Given** no Ollama is running (or WAN would require a pull), **When** a query is issued, **Then** the system returns a fail-fast error (`model unavailable offline` / workstation unavailable) and does not hang waiting for a cloud pull.

---

### User Story 2 - Thin Client Actually Connects (Priority: P1)

An employee runs the thin `.NET MAUI` client on the same LAN (or on `localhost` in dev). The client reads the workstation URL and API key from configuration, discovers its own role from the API, and shows only the actions its role is allowed to see. If the workstation is unreachable, the client surfaces a LAN-offline error and never tries a cloud fallback.

**Why this priority**: The client today is a classlib fallback (`net8.0`, views excluded). `DocumentsApiClient` has no wired base address or key and `LibraryViewModel.IsAdmin` defaults to Employee, so the UI cannot reach the workstation and never enforces the actual role.

**Independent Test**: Start the real API as in US-1. Configure `src/RAGGit.Client.Maui/appsettings.json` with `Workstation:Url` and the `Admin` and `Employee` keys (or per-key in `Api: *`). Launch the MAUI client (or run the view model against a `HttpClient` pointed at the same URL) as Admin: `Upload` is visible and succeeds; `Delete` succeeds. Reconfigure as Employee: `Library` lists the same docs, `Upload` / `Delete` are hidden or disabled and return `403` if attempted. Kill the API → client shows `cannot reach AI workstation` within the query timeout.

**Acceptance Scenarios**:

1. **Given** the client is configured with a valid workstation URL and a valid key, **When** it calls `GET /api/auth/me` (or equivalent), **Then** it learns its role (`Admin` or `Employee`) and renders the UI accordingly without hardcoding Employee.
2. **Given** the workstation is stopped or the LAN path is down, **When** the employee asks a question, **Then** the client fails within the timeout and shows `AI workstation unavailable` / `cannot reach AI workstation` with a retry, and never attempts a cloud call.

---

### User Story 3 - Safe Model Swap (Priority: P2)

A developer or admin changes the configured embed model from `all-minilm` (384) to `nomic-embed-text` (768) (or the reverse) in configuration and restarts the API. The system detects that the existing `library` collection was built with a different dimension and **refuses to start** with an actionable message that names the fix (delete `data/lancedb` or run a re-index), rather than silently producing junk results. A first-request check remains as a backstop for lazy collection creation.

**Why this priority**: `all-minilm` is the dev default (laptop-friendly); `nomic-embed-text` is the prod choice. Today the dimension comes from `VectorDb:VectorSize` (384) and nothing validates it against the on-disk collection.

**Independent Test**: Create a DB with `VectorSize: 384` and ingest one doc. Change config to `768`, restart. Assert startup (or first query) returns a dimension-mismatch error with actionable re-index/wipe guidance, and no data is silently misread. After wiping `data/lancedb`, ingest again with 768 — subsequent queries succeed.

**Acceptance Scenarios**:

1. **Given** a mismatch between configured `VectorSize` and the existing collection's dimension, **When** the API starts (or, as a backstop, the first ingest/query runs), **Then** it fails with a message that names both the configured and stored dimensions and the recovery steps.

---

### Edge Cases

- Ollama is down / model not pulled → the real-path test is skipped (opt-in suite) and the running API returns `503 model unavailable offline` rather than hanging (constitution IV).
- Collection was created with 384 but config says 768 (or vice versa) → dimension guard fires as above.
- `Ollama:Url` points at an unreachable host → startup may succeed but first embed/chat fails fast with a timeout and `503`.
- Legacy `/data/qdrant` still contains a previous vector DB → it is ignored; `VectorDb:Path` is the single source of truth, but a startup log warns about the legacy path.
- Client configuration missing `Workstation:Url` or key → client shows a configuration error at launch rather than attempting unauthenticated calls.
- Corrupted/invalid `pdf`/`docx` → returns `400` with no partial index (carried over but validated here with real parsing).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow the configured embed model to be selected between `all-minilm` (384d) and `nomic-embed-text` (768d) via configuration (`Ollama:EmbedModel` / `VectorDb:VectorSize`), validated at startup and at first embed against the persisted collection's dimension, with an actionable dimension-mismatch error.
- **FR-002**: System MUST provide a real, non-fake integration path (real Ollama + real LanceDB) covering ingest → index → query and retrieval of real cited answers, runnable locally; this path MUST be opt-in so CI remains deterministic with fakes and skips gracefully when `Ollama:Url` is unreachable.
- **FR-003**: System MUST wire the thin `.NET MAUI` client to the workstation via configuration (`Workstation:Url`, API key material from `Api:AdminKey` / `Api:EmployeeKey` or equivalent), with no hard-coded URLs or keys in code.
- **FR-004**: System MUST expose the authenticated user's role to the client (e.g., `GET /api/auth/me` returning `Admin` / `Employee`) so the client can render role-gated UI without hardcoding.
- **FR-005**: System MUST keep Ollama as the **primary** embedding/LLM provider for end-user workstations; the `ONNX` / `LLamaSharp` alternative is **excluded from acceptance** for this feature and remains behind a non-default fallback flag; its tokenizer correctness is a tracked todo and MUST NOT be fixed in this feature.
- **FR-006**: System MUST harden real-document ingestion so corrupted or invalid `pdf`/`docx` returns `400` with no partial index, instead of `500`.
- **FR-007**: System MUST keep the offline invariant: with WAN logically disabled (or Ollama unreachable), queries fail fast with `model unavailable offline` / `503` and never hang waiting for a remote pull; real-path tests MUST verify this.
- **FR-008**: System MUST synchronize shipped docs with reality: `README.md`, `specs/001-offline-mode/quickstart.md`, and the repository's version references MUST be updated to the current API/client/constitution state (MAUI TFMs, LanceDB, `VectorDb:*` keys).

### Key Entities *(include if feature involves data)*

- **Workstation Configuration**: `Ollama:EmbedModel`, `Ollama:ChatModel`, `Ollama:Url`, `VectorDb:Path`, `VectorDb:VectorSize` — the single source of truth for model selection and storage location.
- **Vector Collection**: the persisted LanceDB `library` table, characterized by its configured embedding dimension (384 or 768).
- **Client Session**: the workstation URL + API key + discovered role (`Admin` / `Employee`) held by the thin client; used to gate UI.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On the developer's own laptop with `all-minilm` (384d), a real 50-page PDF uploaded via `POST /api/documents` reaches `Ready` and is searchable in <5 min (wall-clock, measured once and recorded in the feature's verification log).
- **SC-002**: With the loop from 001 running on the same dev laptop (real Ollama + real LanceDB, no fakes), a real `POST /api/query` over LAN/`localhost` returns a cited answer end-to-end; the wall time is measured and recorded, and the "<7s on the reference workstation" figure is explicitly documented as a **production assumption to verify at deploy**, not asserted on the dev laptop.
- **SC-003**: The real integration suite (opt-in) proves zero fakes on the ingest → vector → query core path; when Ollama is absent it is skipped, never red, so CI stays green with the existing fake suite.
- **SC-004**: Switching the configured dimension (384 ↔ 768) against a mismatched collection fails fast with an actionable message (implemented and verified once manually or in an opt-in test).
- **SC-005**: Corrupted `pdf` returns `400` with no partial document retained (verified once with a truncated/invalid sample).

## Clarifications

### Session 2026-09-11

- **Q1 — Client platforms for real run**: Windows desktop (or localhost dev) run is required; mobile on-device (iOS/Android) deferred. So US-2 Independent Test is satisfied by a Windows/localhost client run; mobile LAN behavior is an explicit non-goal for this feature.
- **Q2 — Identity envelope for `GET /api/auth/me`**: Extensible envelope now (`identityType` + `role`, client ignores unknown fields); `002` updates the OpenAPI contract with a MINOR bump. Minimal `{role}`-only shape rejected — would force a breaking change in `004` (would require MAJOR per constitution VII).
- **Q3 — Dimension guard timing**: Fail at **startup** is normative; first-request check is a backstop. Guard is covered by an automated test using a fake/pre-seeded collection (no live Ollama needed in CI). This unifies `spec.md:24 S3`, `spec.md:47 US-3`, `spec.md:55 S1`, `spec.md:72 FR-001`, `spec.md:94 SC-004`.

## Assumptions

- Developer owns a single laptop (8–16GB RAM). `all-minilm:384` + `phi3:mini` is the dev baseline; `nomic-embed-text:768` + `llama3.2:3b` is the production workstation baseline. Chat model on dev is `phi3:mini` for RAM.
- `Ollama:Url` defaults to `http://localhost:11434`; in dev `Workstation:Url` is `http://localhost:5001`.
- ONNX/LLamaSharp remain installed as packages but are **not acceptance** for this feature; tokenizer correctness stays a tracked todo.
- Legacy key `Qdrant:Path` is deprecated but warnings are emitted when it exists and disagrees with `VectorDb:Path`.
- `LanceDB` .NET SDK remains embedded file-backed via `VectorDb:Path` (`lancedb.connect(path)`).
- `txt`/`md` remain supported as in `001`; only `pdf`/`docx` hardening is new here. `xlsx`/OCR is **out of scope** (is `003`).
- Identity in this feature is API-key role material (`Admin` / `Employee`); real per-person accounts are **out of scope** (is `004` per-person history's prerequisite).

## Dependencies

- `001-offline-mode` shipped as `v1.1.0`; its `001` contracts and fakes remain the fast CI gate. `002` adds the opt-in real gate beside it.
- `002` is the prerequisite for `003-ingest-breadth` and `004-identity-accounts` / `005-query-ux`; history/per-person cannot be evaluated until the real loop is proven.
