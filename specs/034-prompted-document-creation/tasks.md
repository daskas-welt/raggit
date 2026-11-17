---
description: "Implementation tasks for description-based document creation"
---

# Tasks: Description-Based Document Creation

**Input**: Design documents from `/specs/034-prompted-document-creation/`

**Prerequisites**: `plan.md`, `spec.md`; supporting documents `research.md`, `data-model.md`, `contracts/`, and `quickstart.md`.

**Tests**: Required by Constitution VI (TDD). Each test task is written and run to confirm failure before its implementation tasks.

**Organization**: Tasks are grouped by user story so the standalone creation MVP can be validated before source-grounding and safety hardening.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel with other marked tasks in its phase because it uses separate files and has no dependency on their unfinished work.
- **[Story]**: User story label matching `spec.md`: US1, US2, or US3.
- Every task includes the repository file path(s) it changes.

## Phase 1: Setup

No project, package, or solution initialization is needed. This feature uses the existing Core, Retrieval, Documents, API, WPF client, and test projects.

---

## Phase 2: Foundational

**Purpose**: Establish the shared content/rendering seam required by all three user stories. Preserve the 033 query-export eligibility and output behavior.

- [X] T001 [P] Add failing draft-generator tests in `tests/unit/DocumentEligibilityTests.cs`: `DocumentGenerator.Generate(DocumentDraft, ...)` accepts a source-free draft, `DocumentEligibility.IsContentEligible(QueryDetail)` continues rejecting citationless saved answers, and a draft over 25 MiB or 5,000 spreadsheet data rows is rejected. Confirm the tests fail before changing `DocumentGenerator`.
- [X] T002 [P] Add failing source-free draft tests in `tests/unit/DocumentRendererTests.cs` for TXT, DOCX, and PDF: each contains the sanitized description title and body, states “No RAGGit sources were used,” and leaves existing `QueryDetail` expectations unchanged. Confirm the tests fail before renderer changes.
- [X] T003 [P] Add failing draft-output tests in `tests/unit/XlsxRendererTests.cs`: source-free prose creates a readable section sheet without an empty Sources sheet; tabular content has meaningful headers and separate cell values. Confirm the tests fail before renderer changes.
- [X] T004 [P] Add failing parser tests in `tests/unit/DocumentTableBuilderTests.cs`: mixed record/prose content is preserved, missing record fields stay empty, and citation markers without a matching source do not become source indexes. Confirm the tests fail before parser changes.
- [X] T005 Add `DocumentDraft` to `src/RAGGit.Core/Models/DocumentDraft.cs` with `Description`, `Body`, and `IReadOnlyList<Citation> Sources`; keep it transient and storage-free.
- [X] T006 Add `IDocumentRenderer.Render(DocumentDraft)` and `DocumentGenerator.Generate(DocumentDraft, DocumentOutputFormat)` in `src/RAGGit.Documents/IDocumentRenderer.cs` and `src/RAGGit.Documents/DocumentGenerator.cs`. Allow zero-source drafts, preserve `Render(QueryDetail)`/`Generate(QueryDetail, ...)` compatibility and its refusal/citation eligibility check, and enforce the 25 MiB / 5,000-row caps for both paths.
- [X] T007 Generalize citation mapping and spreadsheet shaping in `src/RAGGit.Documents/DocumentNaming.cs` and `src/RAGGit.Documents/DocumentTableBuilder.cs` to accept `DocumentDraft`; keep mixed record/prose content and reject or remove citation markers that do not map to `Sources`.
- [X] T008 Update `src/RAGGit.Documents/Renderers/DocxRenderer.cs`, `src/RAGGit.Documents/Renderers/PdfRenderer.cs`, `src/RAGGit.Documents/Renderers/TxtRenderer.cs`, and `src/RAGGit.Documents/Renderers/XlsxRenderer.cs` to render `DocumentDraft` through the shared path. Retain `QueryDetail` render compatibility and its existing source appendix; render an explicit no-source notice instead of an empty appendix for source-free drafts.

**Checkpoint**: Existing QueryDetail exports retain their eligibility, citations, and caps; the new render-input path can produce a valid source-free file in each format.

---

## Phase 3: User Story 1 — Create a document from a description (Priority: P1) — MVP

**Goal**: An Employee or Admin can enter a description, explicitly choose one format, create a new source-free document, and save it locally without an Ask answer.

**Independent Test**: With an empty library, submit a valid description in TXT, PDF, DOCX, and XLSX, save each result, and open each file in its standard application. Confirm the request is not added to Ask history.

### Tests for User Story 1

- [X] T009 [P] [US1] Add failing no-source generation tests in `tests/unit/DocumentGenerationServiceTests.cs`: generation calls the fake local `ILlmClient` when chunks are empty, returns content with no citations, uses the selected format in its prompt, and does not return the Ask refusal `no relevant content found`. Confirm the tests fail before implementation.
- [X] T010 [P] [US1] Add failing empty-library tests in `tests/unit/DocumentCreationPipelineTests.cs`: a valid description and selected format produce a `DocumentDraft` with no sources, and the pipeline does not write query history. Confirm the tests fail before implementation.
- [X] T011 [P] [US1] Add failing endpoint tests in `tests/contract/DocumentCreationContractTests.cs` for unauthenticated/unauthorized access, blank or over-10,000-character descriptions, missing/unsupported format, and successful TXT/PDF/DOCX/XLSX responses. Quote the exact accepted formats `docx`, `xlsx`, `pdf`, `txt` in the test cases; confirm failure before adding the endpoint.
- [X] T012 [P] [US1] Add failing HTTP-client tests in `tests/unit/DocumentCreationApiClientTests.cs` for `POST /api/documents/generate`, JSON `{ description, format }`, binary bytes/filename/content type, safe 400/503 mapping, and caller cancellation. Confirm failure before adding the client.
- [X] T013 [P] [US1] Add failing ViewModel tests in `tests/unit/DocumentCreationViewModelTests.cs`: empty description or null format disables creation; selecting one supported format sends the exact description/format and saves returned bytes through `IFileSaveService`. Confirm failure before adding the ViewModel.
- [X] T014 [P] [US1] Update `tests/client/ScreenLoadTests.cs` to construct `DocumentCreationPage` and assert the `NavCreateDocument` entry and required automation IDs. Confirm the screen test fails before adding the page.

### Implementation for User Story 1

- [X] T015 [P] [US1] Add `DocumentCreationRequest` in `src/RAGGit.Core/Models/DocumentCreationRequest.cs` with required `description` and `format`; enforce “non-whitespace; maximum 10,000 characters” and accept exactly `docx`, `xlsx`, `pdf`, or `txt` case-insensitively.
- [X] T016 [P] [US1] Add `GenerationService.GenerateDocumentAsync(string description, DocumentOutputFormat format, IReadOnlyList<SearchResult> chunks, IReadOnlyDictionary<string, string?>? documentNames = null, CancellationToken cancellationToken = default)` in `src/RAGGit.Retrieval/GenerationService.cs`. For an empty chunk list, compose from the description using the local LLM and return no citations; preserve the existing Q&A `GenerateAsync` behavior.
- [X] T017 [US1] Add `DocumentCreationPipeline.CreateAsync(DocumentCreationRequest request, CancellationToken cancellationToken)` in `src/RAGGit.Retrieval/DocumentCreationPipeline.cs`; use the source-free generation path for the MVP and return a transient `DocumentDraft` without persisting a Query or Document.
- [X] T018 [US1] Add `DocumentCreationController` in `src/RAGGit.Workstation.Api/Controllers/DocumentCreationController.cs` with `POST /api/documents/generate`, Employee/Admin authorization, validation and local-model health check before generation, `DocumentGenerator` rendering, format-correct file response, `Content-Disposition`, `Cache-Control: no-store`, and user-safe 400/422/503/500 mappings.
- [X] T019 [US1] Register `DocumentCreationPipeline` in `src/RAGGit.Workstation.Api/Program.cs`; do not register a query repository dependency or add persistence for creation requests/results.
- [X] T020 [P] [US1] Add `DocumentCreationApiClient.CreateAsync(string description, DocumentOutputFormat format, CancellationToken cancellationToken)` in `src/RAGGit.Client.Core/Services/DocumentCreationApiClient.cs`; reuse `DocumentFile` and map the endpoint's safe errors without changing the 033 `DocumentApiClient`.
- [X] T021 [US1] Register the typed creation client in `src/RAGGit.Client.WPF/App.xaml.cs` with the existing workstation base URL and bearer handler, a 16-minute timeout, and the invalid-config fallback.
- [X] T022 [US1] Add `DocumentCreationViewModel` in `src/RAGGit.Client.Core/ViewModels/DocumentCreationViewModel.cs` with description, nullable selected format, create/save/open commands, busy state, and user-safe status. Save only through `IFileSaveService`.
- [X] T023 [US1] Add `DocumentCreationPage.xaml` and `DocumentCreationPage.xaml.cs` under `src/RAGGit.Client.WPF/Views/Pages/`; use the standard WPF `TextBox`/`ComboBox` themed by WPF-UI, Fluent controls where available, explicit Grid tracks, and the automation IDs in `contracts/ui-contracts.md`.
- [X] T024 [US1] Register the new ViewModel/page in `src/RAGGit.Client.WPF/App.xaml.cs` and add **Create Document** with automation ID `NavCreateDocument` to `src/RAGGit.Client.WPF/ViewModels/MainWindowViewModel.cs`.

**Checkpoint**: With no relevant library sources, a signed-in user can create and locally save all four formats. Ask, query history, and the 033 exporter remain unchanged.

---

## Phase 4: User Story 2 — Create a document that uses relevant library material (Priority: P2)

**Goal**: Use relevant uploaded sources when the description depends on library material; cite only retrieved chunks, disclose missing/conflicting facts, and avoid unrelated or other-person records.

**Independent Test**: With a known uploaded source, request a document based on it and verify its listed sources and factual claims. Repeat with unrelated content, conflicting values, and same-template records for two people; no unrelated or other-person facts may appear.

### Tests for User Story 2

- [X] T025 [P] [US2] Add failing retrieval/source-map tests in `tests/unit/DocumentCreationPipelineSourceTests.cs` using a fake embedder, vector store, and `IDocumentRepository`: use configured `Retrieval:TopK`/`Retrieval:MinScore`, attach names/excerpts only for retrieved chunks, and reject a cited chunk ID outside the retrieved set. Confirm failure before pipeline changes.
- [X] T026 [P] [US2] Add failing source-prompt tests in `tests/unit/DocumentGenerationPromptTests.cs`: retrieved text is marked reference-only, the prompt forbids following embedded instructions, applies the same-person rule, requests citations, and tells the model to disclose missing/conflicting values. Assert no `FallbackToTopChunk` citation is used when the model emits no valid citation.
- [X] T027 [P] [US2] Add failing source-backed endpoint tests in `tests/contract/DocumentCreationSourceContractTests.cs`: seed relevant chunks and document names through `TestApiFactory`, request a file, and assert the correct MIME type, source appendix, and unchanged query-history/library counts. Confirm failure before source integration.
- [X] T028 [P] [US2] Add deterministic end-to-end integration coverage in `tests/integration/DocumentCreationEvalTests.cs` for a settled source document through retrieve → compose → render, using the existing fake model and settled-document helper (not `Task.Delay`).

### Implementation for User Story 2

- [X] T029 [US2] Extend `GenerationService.GenerateDocumentAsync` in `src/RAGGit.Retrieval/GenerationService.cs` to use relevant chunks as untrusted evidence, cite only their chunk IDs, preserve Greek/English behavior, disclose gaps/conflicts, and fail source-backed generation when no valid cited chunk is returned.
- [X] T030 [US2] Extend `DocumentCreationPipeline` in `src/RAGGit.Retrieval/DocumentCreationPipeline.cs` to retrieve by description via `RetrievalService.RetrieveAsync` using `DefaultTopK`, resolve source names through `IDocumentRepository`, and map only validated citations into `DocumentDraft.Sources`; when no relevant chunks are found, retain the US1 source-free path.

**Checkpoint**: A source-backed document identifies only relevant retrieved sources; source-free requests still work, and same-template records do not transfer facts.

---

## Phase 5: User Story 3 — Save a complete and usable result safely (Priority: P2)

**Goal**: Cancellation, failures, and repeated submissions never leave partial files, stale UI state, or server-side generated-document records.

**Independent Test**: Generate each format, cancel generation/save, force local-model and save failures, then verify no file is written to an unchosen path, no stale Open action remains, and query history/library counts are unchanged.

### Tests for User Story 3

- [X] T031 [P] [US3] Add failing ViewModel tests in `tests/unit/DocumentCreationViewModelTests.cs` for cancellation before save, save-dialog cancellation, save exception, repeated submission while busy, and clearing stale bytes/path after a failed request. Confirm failure before lifecycle changes.
- [X] T032 [P] [US3] Add failing failure/retention tests in `tests/contract/DocumentCreationFailureContractTests.cs`: model unavailable and renderer failure return safe JSON with no file bytes; creation leaves query history, uploaded documents, and the content store unchanged; successful responses use `Cache-Control: no-store`. Confirm failure before controller hardening.
- [X] T033 [P] [US3] Add `DocumentCreationUiTests` in `tests/client/DocumentCreationUiTests.cs` for keyboard-reachable description/format/create/cancel/save/open controls, stable automation IDs, busy-state disabling, and return to idle after save cancellation.

### Implementation for User Story 3

- [X] T034 [US3] Add a cancellation source, `CancelCommand`, busy guard, and cleanup of transient document bytes/path to `src/RAGGit.Client.Core/ViewModels/DocumentCreationViewModel.cs`; pass cancellation through API and save calls, and do not save after cancellation.
- [X] T035 [US3] Harden `DocumentCreationController` in `src/RAGGit.Workstation.Api/Controllers/DocumentCreationController.cs` to handle caller cancellation and model/renderer failures with safe status responses, metadata-only logs, no partial file response, and no persistent writes.
- [X] T036 [US3] Update `src/RAGGit.Client.WPF/Views/Pages/DocumentCreationPage.xaml.cs` and `src/RAGGit.Client.WPF/Views/Pages/DocumentCreationPage.xaml` to cancel in-flight work on navigation away, disable repeat submissions, clear stale output on failure, and show the saved filename/Open action only after a successful save.

**Checkpoint**: Save cancellation, generation cancellation, and failures leave no unwanted file or stale saved state; generated requests/results never enter RAGGit history or the shared library.

---

## Phase 6: Polish & Cross-Cutting Verification

- [X] T037 Run `dotnet build RAGGit.Server.slnf -c Release`, Release unit and contract tests, the offline integration subset, and on Windows `dotnet build RAGGit.sln -c Release -p:Platform=x64` plus `dotnet test tests/client/RAGGit.Tests.Client.csproj -c Release`; after XAML changes run `dotnet csharpier format .` then `dotnet csharpier check .`. Record the verified commands and results in `specs/034-prompted-document-creation/quickstart.md`.
- [X] T038 Human acceptance: run the manual SC-002 (30 source-backed documents), SC-003 (20 same-template/different-person requests), SC-004 (five-employee walkthrough), and SC-007 (15-minute timing) reviews in `specs/034-prompted-document-creation/quickstart.md`; record deviations before release. Owner reported all checks passed on 2026-11-17.
- [X] T039 Add opt-in `RequiresOllama` evaluation cases for relevant-source use, irrelevant-source exclusion, same-template/different-person isolation, and conflicting source values; skip when Ollama is unavailable. This is the automated real-model evidence for SC-002/SC-003. Added in `tests/integration/DocumentCreationRealModelTests.cs`; run with `OLLAMA_CHAT_MODEL` / `OLLAMA_EMBED_MODEL` set to installed models.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No tasks; current projects, dependencies, and formatting tools are already in place.
- **Foundational (Phase 2)**: Must finish before any user story; introduces the transient draft and renderer seam while pinning 033 regressions.
- **User Story 1 (Phase 3)**: Depends on Phase 2; delivers the description-only MVP.
- **User Story 2 (Phase 4)**: Depends on US1's request/pipeline/render path; extends that path with retrieved sources and citations.
- **User Story 3 (Phase 5)**: Depends on US1's client/API path; hardens cancellation, failure, and no-retention behavior.
- **US2 and US3**: Can proceed in parallel after US1 because their implementation files are in Retrieval versus API/Client, with tests in separate files.
- **Polish (Phase 6)**: Depends on the stories selected for release.

### User Story Dependencies

- **US1 (P1)**: Depends only on Foundational; no source documents or previous Ask answer are required.
- **US2 (P2)**: Depends on the US1 creation endpoint and pipeline; it adds library retrieval and source attribution.
- **US3 (P2)**: Depends on the US1 save flow; it hardens the existing creation lifecycle. It does not depend on US2 and can run alongside US2 after US1.

### Within Each User Story

- Write tests first and run them to confirm failure before implementing the behavior.
- Complete shared model/render tests before endpoint/UI integration.
- Complete retrieval and model prompt work before source-backed endpoint/eval verification.
- Complete ViewModel cancellation tests before changing cancellation lifecycle.
- Run each story's independent test before moving to the next checkpoint.

### Parallel Opportunities

- Foundational tests T001–T004 use separate unit-test files.
- US1 tests T009–T014 use separate service, pipeline, contract, client, ViewModel, and client-screen files.
- US2 tests T025–T028 use separate unit, contract, and integration files.
- US3 tests T031–T033 use separate ViewModel, contract, and client files.
- After US1, US2 retrieval work and US3 API/client hardening can proceed in parallel if separate owners keep to the listed files.

## Parallel Example: User Story 1

```text
Run the red test tasks in parallel:
T009 tests/unit/DocumentGenerationServiceTests.cs
T010 tests/unit/DocumentCreationPipelineTests.cs
T011 tests/contract/DocumentCreationContractTests.cs
T012 tests/unit/DocumentCreationApiClientTests.cs
T013 tests/unit/DocumentCreationViewModelTests.cs
T014 tests/client/ScreenLoadTests.cs
```

## Parallel Example: User Story 2

```text
Run the source-specific red tests in parallel:
T025 tests/unit/DocumentCreationPipelineSourceTests.cs
T026 tests/unit/DocumentGenerationPromptTests.cs
T027 tests/contract/DocumentCreationSourceContractTests.cs
T028 tests/integration/DocumentCreationEvalTests.cs
```

## Parallel Example: User Story 3

```text
Run the safety red tests in parallel:
T031 tests/unit/DocumentCreationViewModelTests.cs
T032 tests/contract/DocumentCreationFailureContractTests.cs
T033 tests/client/DocumentCreationUiTests.cs
```

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete the foundational transient-content and renderer seam.
2. Implement source-free description generation, the binary endpoint, and the Create Document page.
3. Run US1's unit, contract, and Windows screen tests; verify all four saved formats open.
4. Stop at the US1 checkpoint for an independently demonstrable MVP.

### Incremental Delivery

1. Complete US1 and validate prompt-only creation.
2. Add US2 retrieval/citations; verify relevant sources, no unrelated facts, and same-person isolation.
3. Add US3 cancellation/failure/no-retention checks.
4. Run the full build, offline subset, client screen tests, and quickstart scenarios.

## Notes

- `[P]` tasks touch separate files and have no unfinished dependency.
- All story tasks use `[US1]`, `[US2]`, or `[US3]`; setup/foundational/polish tasks do not.
- Existing upload limits/status are unchanged; integration tests must wait for settled documents via the repository helper.
- Do not modify feature 033's Ask export semantics or add a new project, package, model service, or persistent store.
