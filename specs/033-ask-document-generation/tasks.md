# Tasks: Ask-View Document Output

**Input**: Design documents from `/specs/033-ask-document-generation/`

**Prerequisites**: `plan.md` (required), `spec.md` (required), `research.md`, `data-model.md`, `contracts/`, `quickstart.md`

**Tests**: Required by Constitution VI (TDD). Write each test task first, confirm it fails for the right reason, then implement. The client screen tests are part of the same gate.

**Organization**: Tasks are grouped by user story so each can be implemented and validated independently. The assessed from-scratch tier is out of scope.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no incomplete dependency)
- **[Story]**: `US1`, `US2`, `US3`, `US4` for user-story phases only
- Every task includes an exact repository-relative file path

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the document library, wire it into the solution, and add the client save seam.

- [x] T001 Create `src/RAGGit.Documents/RAGGit.Documents.csproj` targeting `net10.0` with `DocumentFormat.OpenXml` 3.5.1 and `SkiaSharp` 4.156.1 (plus Win32/Linux native assets), and embed `Assets/` as resources.
- [x] T002 Add `src/RAGGit.Documents/RAGGit.Documents.csproj` to `RAGGit.sln` and to the project list in `RAGGit.Server.slnf`.
- [x] T003 [P] Add project references: `src/RAGGit.Workstation.Api/RAGGit.Workstation.Api.csproj` → `RAGGit.Documents`; `tests/unit/RAGGit.Tests.Unit.csproj` → `RAGGit.Documents`; `tests/contract/RAGGit.Tests.Contract.csproj` → `RAGGit.Documents`.
- [x] T004 [P] Add `src/RAGGit.Documents/Assets/DejaVuSans.ttf`, `DejaVuSans-Bold.ttf` and `LICENSE-DejaVu.txt`; mark both fonts as `EmbeddedResource` in `src/RAGGit.Documents/RAGGit.Documents.csproj`.
- [x] T005 [P] Add `IFileSaveService` and the `InMemoryFileSaveService` test double in `src/RAGGit.Client.Core/Services/IFileSaveService.cs` with `SaveAsync(suggestedFileName, bytes, contentType)` returning the chosen path or null, plus `OpenAsync(path)`.
- [x] T006 [P] Add `tests/unit/DocumentTestData.cs` with builders for `QueryDetail` and `HistoryCitation` (owned query, Greek/English answers, zero/one/many citations) reused by unit and contract tests.
- [x] T007 Verify `dotnet build RAGGit.Server.slnf -c Release` and `dotnet csharpier check .` still pass after setup; record the result in the feature's verification notes (`specs/033-ask-document-generation/quickstart.md` is the reference).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared wire types, detection, eligibility and rendering models that every story uses. No story UI or endpoint work begins before this phase is green.

**CRITICAL**: Complete this phase before any user story phase.

### Tests first

- [x] T008 [P] Write failing tests for `DocumentRequestDetector` in `tests/unit/DocumentDetectionTests.cs`: Word/docx, Excel/xlsx/spreadsheet, PDF, text/txt, Greek equivalents, no format, multiple formats → ambiguous, PowerPoint/pptx/presentation/image/email → unsupported, and "what does the text say?" → no format (no intent).
- [x] T009 [P] Write failing tests for `TextScript.IsGreek` and the `AnswerLanguage.IsGreek` delegation in `tests/unit/TextScriptTests.cs` (Greek block, English, mixed, empty).
- [x] T010 [P] Write failing tests for `DocumentFileName`, `DocumentLabels` and `DocumentCitationMapper` in `tests/unit/DocumentNamingTests.cs`: strip leading command/format words, trim punctuation, keep Greek, cap at 80 characters, fallback `document`, Greek/English labels, stable citation numbering.
- [x] T011 [P] Write failing tests for `DocumentEligibility` in `tests/unit/DocumentEligibilityTests.cs`: exactly `no relevant content found` is ineligible; zero citations ineligible; one or more citations eligible; 25 MiB cap; 5 000-row cap.
- [x] T012 [P] Write failing contract tests for the additive query id in `tests/contract/QueryDocumentIdContractTests.cs`: the classic answer and the streamed `answer` event carry the persisted `queryId`, and every existing field is unchanged.

### Implementation

- [x] T013 [P] Add `src/RAGGit.Core/Models/DocumentOutputFormat.cs` with the four wire values (`docx`, `xlsx`, `pdf`, `txt`), their content types and extensions, using `[JsonStringEnumMemberName]` like `QueryStage`.
- [x] T014 [P] Add `src/RAGGit.Core/Models/DocumentRequest.cs` with `string format` validated against exactly `docx|xlsx|pdf|txt`, and `DocumentFormatDetection` with `Format` (`DocumentOutputFormat?`), `UnsupportedFormat` (`string?`) and `IsAmbiguous` (`bool`).
- [x] T015 [P] Add `src/RAGGit.Core/TextScript.cs` and make `src/RAGGit.Retrieval/AnswerLanguage.cs` delegate `IsGreek` to it without changing behaviour.
- [x] T016 [P] Add `src/RAGGit.Core/Models/DocumentRequestDetector.cs` implementing the conservative rules from T008, including Greek format/verb tokens and the no-format/ambiguous/unsupported outcomes.
- [x] T017 [P] Add `Guid? QueryId` to `src/RAGGit.Core/Models/QueryRequest.cs` (`QueryResponse`) and `src/RAGGit.Core/Models/QueryStreamEvent.cs`; copy it in `FromAnswer` so the streamed terminal event carries it.
- [x] T018 Change `SaveQueryAsync` in `src/RAGGit.Workstation.Api/Controllers/QueryController.cs` to return the persisted `Guid`; set `QueryId` on the classic response and on the stream's terminal answer event (depends on T017).
- [x] T019 [P] Add the shared rendering models in `src/RAGGit.Documents/Models/DocumentModels.cs`: `GeneratedDocument` (format, file name, content type, bytes, title, sources, byte count), `DocumentSource` (index, document id, document name, chunk id, ordinal, excerpt), `DocumentSection`, `DocumentRow` and `DocumentCell` (text/number/date kind).
- [x] T020 Add `src/RAGGit.Documents/DocumentFileName.cs` (title/filename derivation and sanitization), `DocumentLabels.cs` (Greek/English headings) and `DocumentCitationMapper.cs` (answer GUID → 1-based source index; source appendix data). Depends on T019.
- [x] T021 [P] Add `src/RAGGit.Documents/DocumentEligibility.cs` enforcing: answer is not exactly `no relevant content found`; `Citations.Count > 0`; rendered bytes ≤ 25 MiB; table rows ≤ 5 000.
- [x] T022 Run `dotnet test tests/unit/RAGGit.Tests.Unit.csproj -c Release --filter "FullyQualifiedName~Document"` and `dotnet test tests/contract/RAGGit.Tests.Contract.csproj -c Release --filter "FullyQualifiedName~QueryDocumentId"`; fix the foundational code until green.

**Checkpoint**: Detection, naming, eligibility and the query id are stable; story work can begin.

---

## Phase 3: User Story 1 — Ask for a document, receive one (Priority: P1) — MVP

**Goal**: An employee asks for a Word, PDF or text document and receives a file whose content matches the grounded answer, with a sources appendix, saved through the OS dialog.

**Independent Test**: Ask for a Word document about a known subject, save it, open it in Word, and compare its title, body and sources with the answer; repeat for PDF and text. Ask for something the library cannot support and confirm no file is offered.

### Tests for US1 (write first; confirm red)

- [x] T023 [P] [US1] Add `tests/unit/DocxRendererTests.cs`: open the produced DOCX with OpenXml; assert title, answer body, sources appendix, every cited chunk id, Greek text, and no invented facts.
- [x] T024 [P] [US1] Add `tests/unit/PdfRendererTests.cs`: read the produced PDF with PdfPig; assert title, body, sources, Greek glyphs, and each cited chunk id.
- [x] T025 [P] [US1] Add `tests/unit/TxtRendererTests.cs`: decode UTF-8; assert title, body, sources and cited chunk ids.
- [x] T026 [P] [US1] Add `tests/contract/QueryDocumentsContractTests.cs`: authorization; `404` for another user's query; `400` for an invalid format; `422` for a refusal/citationless answer; correct `Content-Type` and `Content-Disposition` for docx/pdf/txt; no LLM call.
- [x] T027 [P] [US1] Add `tests/integration/DocumentGenerationTests.cs`: ask, auto-detect the format, render docx/pdf/txt, read each file back, and confirm the refusal returns no file.
- [x] T028 [P] [US1] Add `tests/unit/QueryViewModelDocumentTests.cs`: auto-produce after a terminal answer; save success; save cancel; save failure; busy state; refusal produces no file.

### Implementation for US1

- [x] T029 [P] [US1] Add `src/RAGGit.Documents/IDocumentRenderer.cs` with `Render(QueryDetail detail, DocumentOutputFormat format)` returning `GeneratedDocument`.
- [x] T030 [P] [US1] Implement `src/RAGGit.Documents/Renderers/DocxRenderer.cs`: title, answer paragraphs, numbered citation replacements, sources table (document name, ordinal, chunk id, excerpt), Greek text.
- [x] T031 [P] [US1] Implement `src/RAGGit.Documents/Renderers/PdfRenderer.cs`: SkiaSharp PDF output with the embedded DejaVu regular/bold typefaces, title, wrapped answer, sources section, Greek text.
- [x] T032 [P] [US1] Implement `src/RAGGit.Documents/Renderers/TxtRenderer.cs`: UTF-8 title, answer and sources appendix with the same numbering.
- [x] T033 [US1] Add `src/RAGGit.Documents/DocumentGenerator.cs`: validate eligibility, dispatch to the renderer, enforce the 25 MiB/5 000-row caps and return `GeneratedDocument`; xlsx remains unsupported until US2.
- [x] T034 [US1] Add `src/RAGGit.Workstation.Api/Controllers/QueryDocumentsController.cs` for `POST /api/queries/{id:guid}/documents`: map the string format, `GetDetailAsync(sub, id)`, `404` not owned, `422` refusal/citationless/too large, `400` invalid, return `File(bytes, contentType, fileName)`; never call the LLM or persist.
- [x] T035 [US1] Register `DocumentGenerator` and the docx/pdf/txt renderers in `src/RAGGit.Workstation.Api/Program.cs`.
- [x] T036 [P] [US1] Implement `src/RAGGit.Client.Core/Services/DocumentApiClient.cs`: POST the format, read `Content-Type` and `Content-Disposition`, return bytes + suggested filename, map `400/404/422/500` and network errors to the existing vocabulary.
- [x] T037 [US1] Add `DocumentApiClient`, `IFileSaveService` and document busy/status state to the constructor and properties of `src/RAGGit.Client.Core/ViewModels/QueryViewModel.cs`.
- [x] T038 [US1] Implement the auto-produce flow in `src/RAGGit.Client.Core/ViewModels/QueryViewModel.cs`: detect the format before submitting, use the terminal answer's `QueryId`, skip refusals/citationless answers, request bytes, open the save dialog, handle cancel/error, and expose the saved path.
- [x] T039 [P] [US1] Implement `src/RAGGit.Client.WPF/Services/WpfFileSaveService.cs`: `SaveFileDialog`, suggested name/extension/filter, write to a sibling temp file, `File.Move(..., overwrite: true)`, delete the temp on failure, and `OpenAsync(path)`.
- [x] T040 [US1] Register `DocumentApiClient` and `IFileSaveService` in `src/RAGGit.Client.WPF/App.xaml.cs`.
- [x] T041 [P] [US1] Add `tests/client/DocumentAutoProduceUiTests.cs` proving the Ask page/chat control load with the new auto-produce UI and report no XAML or binding errors (written before the UI; it must fail first).
- [x] T042 [US1] Add the auto-produce progress/status and save affordance to `src/RAGGit.Client.WPF/Components/ChatControl.xaml`, `src/RAGGit.Client.WPF/Components/ChatControl.xaml.cs` and `src/RAGGit.Client.WPF/Views/Pages/QueryPage.xaml` using WPF-UI controls and grid gutters (no layout margins).

**Checkpoint**: US1 works independently for docx, pdf and txt; US2/US3/US4 can follow.

---

## Phase 4: User Story 2 — Spreadsheets that behave like spreadsheets (Priority: P2)

**Goal**: An Excel request produces a real table: one header row, one row per record, empty cells when the library has no value, and typed dates/numbers; prose falls back to titled sections.

**Independent Test**: Ask for a spreadsheet listing known records; open it in Excel; confirm the header row, one row per record, empty absent fields, and sortable/calculable dates and amounts.

### Tests for US2 (write first; confirm red)

- [x] T043 [P] [US2] Add `tests/unit/DocumentTableBuilderTests.cs`: record shape, section shape, missing labels empty (never inferred), every documented date/amount format (ISO, `dd/MM/yyyy`, `d/M/yyyy`, `dd-MM-yyyy`, `d.M.yyyy`, Greek month names, `.`/`,` separators, `€`/`$`/`£`, percentages, parenthesised negatives), the day-first ambiguity rule, the 5 000-row cap, and Greek labels.
- [x] T044 [P] [US2] Add `tests/unit/XlsxRendererTests.cs`: open with OpenXml; header row, one row per record, empty cells, a date/number cell with the correct number format for every documented format, unrecognized text staying text, and sources.
- [x] T045 [P] [US2] Add `tests/contract/QueryDocumentsXlsxContractTests.cs`: xlsx content type, filename, `200`, ownership and error parity with US1.
- [x] T046 [P] [US2] Add `tests/integration/DocumentSpreadsheetTests.cs`: ask for a spreadsheet, open it, assert typed cells and no invented values.

### Implementation for US2

- [x] T047 [P] [US2] Implement `src/RAGGit.Documents/DocumentTableBuilder.cs`: parse repeated `label: value` blocks into a record table (union of labels, one row per record, empty missing values), otherwise emit titled sections; recognize dates/amounts deterministically; never infer a value.
- [x] T048 [P] [US2] Implement `src/RAGGit.Documents/Renderers/XlsxRenderer.cs`: header row, one row per record, typed `Date`/`Number` cells with formats, empty cells, sources column/section.
- [x] T049 [US2] Register `XlsxRenderer` in `src/RAGGit.Documents/DocumentGenerator.cs` and `src/RAGGit.Workstation.Api/Program.cs`; remove the xlsx "unsupported" branch.
- [x] T050 [US2] Confirm Excel is covered by the shared detector/auto-produce path in `src/RAGGit.Core/Models/DocumentRequestDetector.cs` and `src/RAGGit.Client.Core/ViewModels/QueryViewModel.cs`; add the missing test case to `tests/unit/DocumentDetectionTests.cs`.

**Checkpoint**: US2 works independently; US1 output is unchanged.

---

## Phase 5: User Story 3 — Any finished answer can become a file (Priority: P3)

**Goal**: Every eligible answer in the conversation or reopened from History can be saved as Word, Excel, PDF or text, and the file matches the on-screen answer and sources.

**Independent Test**: Take an ordinary answer, choose each of the four formats from the answer, save, and compare each file with the answer. Repeat from the Query Detail page.

### Tests for US3 (write first; confirm red)

- [x] T051 [P] [US3] Add `tests/unit/ChatMessageDocumentTests.cs`: `QueryId` from live/history, `CanSave` false for refusals and zero-citation answers, true for eligible answers, `SavedPath` change notification.
- [x] T052 [P] [US3] Add `tests/unit/QueryViewModelSaveAsTests.cs`: per-message Save as for all four formats, history message save, busy/one-at-a-time, cancel, failure, open action.
- [x] T053 [P] [US3] Add `tests/unit/QueryDetailViewModelDocumentTests.cs`: save from a reopened answer using `QueryDetail.Id`, busy/cancel/error.
- [x] T054 [P] [US3] Add `tests/contract/QueryDocumentsAnyAnswerContractTests.cs`: any owned query can be rendered, not only auto-produced ones; another user's query is `404`.
- [x] T055 [P] [US3] Add `tests/client/DocumentSaveUiTests.cs`: Save as flyout with four entries, automation IDs, Saved/Open state, disabled for refusals, page construction and binding checks.

### Implementation for US3

- [x] T056 [US3] Add `QueryId`, `SavedPath`, `CanSave` and change notification to `src/RAGGit.Client.Core/Models/ChatMessage.cs`; `CanSave` is `QueryId != null && text != exactly "no relevant content found" && Citations.Count > 0`.
- [x] T057 [US3] Add `DocumentSaveRequest` and the per-message Save as command/state to `src/RAGGit.Client.Core/ViewModels/QueryViewModel.cs`.
- [x] T058 [US3] Set `QueryId` on messages loaded from History in `src/RAGGit.Client.Core/ViewModels/QueryViewModel.cs` (`LoadHistoryAsync`).
- [x] T059 [US3] Add the Save as flyout (Word/Excel/PDF/Text), Saved state and Open action to `src/RAGGit.Client.WPF/Components/ChatControl.xaml` and `ChatControl.xaml.cs`.
- [x] T060 [US3] Add Save as to `src/RAGGit.Client.Core/ViewModels/QueryDetailViewModel.cs` and `src/RAGGit.Client.WPF/Views/Pages/QueryDetailPage.xaml`.
- [x] T061 [US3] Add the Open action implementation to `src/RAGGit.Client.WPF/Services/WpfFileSaveService.cs` and wire it through the view models; no temp file remains.
- [x] T062 [US3] Register/update the document client, save service and detail view model in `src/RAGGit.Client.WPF/App.xaml.cs`.

**Checkpoint**: US1, US2 and US3 work independently.

---

## Phase 6: User Story 4 — Documents tell the truth and stay private (Priority: P4)

**Goal**: Every document is supported by the sources it lists, never mixes another person's record, never enters the library, and fails cleanly without a partial file.

**Independent Test**: Generate documents about people who share a record template, compare every statement/cell with the cited sources, and confirm the library is unchanged and no file remains after a cancelled or failed save.

### Tests for US4 (write first; confirm red for the failure/UI cases, then keep the trust/privacy tests as acceptance gates)

- [x] T063 [P] [US4] Add `tests/unit/DocumentTrustTests.cs`: answer text preserved (modulo citation numbering), every cited chunk id in the appendix, no invented facts, sources ordered as cited.
- [x] T064 [P] [US4] Add `tests/integration/DocumentIsolationTests.cs`: same-template records about different people produce no cross-record content.
- [x] T065 [P] [US4] Add `tests/integration/DocumentPrivacyTests.cs`: the library, content store and LanceDB are unchanged; history rows are unchanged; generated bytes are not retained server-side.
- [x] T066 [P] [US4] Add `tests/contract/QueryDocumentsFailureContractTests.cs`: refusal/citationless → `422 no document available`; too large → `422 document too large`; invalid format → `400`; renderer failure → `500` with no bytes.
- [x] T067 [P] [US4] Add `tests/unit/QueryViewModelDocumentErrorTests.cs`: unsupported format message, save cancel, renderer failure, workstation unavailable, no partial file.
- [x] T068 [P] [US4] Add `tests/unit/DocumentUnsupportedFormatTests.cs`: PowerPoint/presentation/image/email are reported as unsupported and never produce a file.

### Implementation for US4

- [x] T069 [US4] Wire the eligibility and 25 MiB/5 000-row predicates from `DocumentEligibility` into `src/RAGGit.Documents/DocumentGenerator.cs` and map them to the documented `422 no document available` / `422 document too large` responses in `src/RAGGit.Workstation.Api/Controllers/QueryDocumentsController.cs`.
- [x] T070 [US4] Add safe logging to `src/RAGGit.Workstation.Api/Controllers/QueryDocumentsController.cs`: query id, format, byte count and duration only; never answer/citation text.
- [x] T071 [US4] Add the unsupported-format and error/cancel UI states to `src/RAGGit.Client.Core/ViewModels/QueryViewModel.cs` and `src/RAGGit.Client.WPF/Components/ChatControl.xaml`.
- [x] T072 [US4] Harden `src/RAGGit.Client.WPF/Services/WpfFileSaveService.cs` for the no-partial-file rule: temp write, atomic move, temp deletion on any failure, and a clear locked-file message.
- [x] T073 [US4] Run the offline subset (`QueryOfflineTests|OfflineIdentityTests|OfflineHistoryTests|CrossUserIsolationTests`) against `tests/integration/RAGGit.Tests.Integration.csproj` and confirm it remains green.

**Checkpoint**: US4 constraints hold across all formats and surfaces.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, formatting, full gates and the measurable/observational checks that cannot live in unit tests.

- [x] T074 [P] Update `AGENTS.md` with the new `RAGGit.Documents` project, the PDF dependency/font asset, the `POST /api/queries/{id}/documents` endpoint and the additively returned `queryId`.
- [ ] T075 [P] Record the SC-006 (≤15% extra workstation time), SC-007 (4/5 employees unaided) and SC-010 (cancelled/failed/unsupported leave zero files) measurements in `specs/033-ask-document-generation/verification.md`.
- [ ] T076 [P] Add the SC-003 trust audit to `specs/033-ask-document-generation/verification.md`: generate 30 documents across the four formats from the known corpus, check every statement and cell against the sources each document lists, and record the sample table (document id, format, sources checked, unsupported statements).
- [ ] T077 [P] Add the SC-004 isolation audit to `specs/033-ask-document-generation/verification.md`: generate 20 documents about different people who share a record template and record zero cross-record content.
- [ ] T078 [P] Add the SC-001/SC-005 sample to `specs/033-ask-document-generation/verification.md`: 20 requests spread across the four formats (open without repair) and 20 answers saved in all four formats (content and sources match the on-screen answer); record the counts and any failures.
- [x] T079 Run `dotnet csharpier format .` and `dotnet csharpier check .` from `RAGGit.sln`; resolve every C# and XAML formatting failure.
- [x] T080 Run the full gate: `dotnet build RAGGit.sln -c Release -p:Platform=x64`, unit, contract, integration and client tests; fix regressions in `RAGGit.sln`.
- [ ] T081 Validate `specs/033-ask-document-generation/quickstart.md` end to end, including the manual scenarios, the sampled audits and the troubleshooting table.
- [x] T082 Review every FR-001…FR-025 and SC-001…SC-010 in `specs/033-ask-document-generation/spec.md` against the implementation; record any deliberate assumption (notably citationless answers are ineligible) in `specs/033-ask-document-generation/verification.md`.

---

## Dependencies & Execution Order

### Phase Dependencies

Story completion order: **Setup → Foundational → US1 (MVP) → US2 → US3 → US4 → Polish**. US2–US4 may proceed in parallel once their listed prerequisites are met, but the priority order is the default.

- **Setup (Phase 1)**: no dependencies; T001–T006 can proceed in parallel where marked, T002 depends on T001, T007 depends on all setup tasks.
- **Foundational (Phase 2)**: depends on Setup; T008–T012 are tests and must precede T013–T021; T018 depends on T017; T020 depends on T019; T022 depends on all foundational implementation.
- **US1 (Phase 3)**: depends on Foundational; T023–T028 are tests and must precede implementation; T029 is the renderer seam; T030–T032 can run in parallel; T033 depends on them; T034 depends on T033; T036 can run in parallel with T030–T035; T037/T038 depend on T036; T039 can run in parallel; T040 depends on T036/T039; T041 depends on T038; T042 depends on T041.
- **US2 (Phase 4)**: depends on Foundational and US1 (renderer seam/controller); tests T043–T046 first; T047/T048 in parallel; T049 depends on them; T050 is the detector/VM parity check.
- **US3 (Phase 5)**: depends on US1 (API client/save service/endpoint) and US2 for Excel; tests T051–T055 first; T056/T057/T058 sequential on the same `QueryViewModel`/`ChatMessage` files; T059/T060/T061/T062 depend on them.
- **US4 (Phase 6)**: depends on US1–US3; tests T063–T068 first; T069/T070/T071/T072 depend on them; T073 is the final offline gate.
- **Polish (Phase 7)**: depends on all desired stories; T076–T078 are the sampled audits and T079–T082 are the release gate. Run the audits before the final FR/SC review (T082).

### User Story Dependencies

- **US1 (P1)**: no dependency on other stories; MVP.
- **US2 (P2)**: depends on US1's renderer seam and endpoint, but is independently testable through the xlsx path.
- **US3 (P3)**: depends on US1's client/API path; adds the per-message and History surfaces.
- **US4 (P4)**: depends on all three to test the trust constraints across formats and surfaces.

### Within Each Story

- Tests are written and confirmed failing before implementation.
- Models/shared types before services; services before endpoints; endpoint before client integration; client integration before UI.
- Commit after each task or logical group.

### Parallel Opportunities

- Setup: T003–T006.
- Foundational tests: T008–T012; foundational types: T013–T017, T019–T021.
- US1 renderer tests: T023–T025; renderers: T030–T032; API client and save service: T036/T039.
- US2 table tests: T043–T046; table builder and xlsx renderer: T047/T048.
- US3 model/VM/detail/contract/UI tests: T051–T055; UI/VM/detail work after their tests.
- US4 trust/isolation/privacy/failure/error tests: T063–T068.
- Polish documentation tasks: T074/T075; sampled audits: T076–T078.

---

## Parallel Example: User Story 1

```text
# Tests first (all different files):
Task: "Write DocxRenderer tests in tests/unit/DocxRendererTests.cs"
Task: "Write PdfRenderer tests in tests/unit/PdfRendererTests.cs"
Task: "Write TxtRenderer tests in tests/unit/TxtRendererTests.cs"
Task: "Write QueryDocuments contract tests in tests/contract/QueryDocumentsContractTests.cs"

# Then the three renderers can run in parallel:
Task: "Implement DocxRenderer in src/RAGGit.Documents/Renderers/DocxRenderer.cs"
Task: "Implement PdfRenderer in src/RAGGit.Documents/Renderers/PdfRenderer.cs"
Task: "Implement TxtRenderer in src/RAGGit.Documents/Renderers/TxtRenderer.cs"
```

## Parallel Example: User Story 2

```text
Task: "Write table builder tests in tests/unit/DocumentTableBuilderTests.cs"
Task: "Write XlsxRenderer tests in tests/unit/XlsxRendererTests.cs"
Task: "Implement DocumentTableBuilder in src/RAGGit.Documents/DocumentTableBuilder.cs"
Task: "Implement XlsxRenderer in src/RAGGit.Documents/Renderers/XlsxRenderer.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Setup (Phase 1).
2. Complete Foundational (Phase 2); this blocks every story.
3. Complete US1 (Phase 3): docx, pdf and text from a grounded answer, auto-produced and saved through the OS dialog.
4. **STOP and VALIDATE** US1 independently with `tests/unit`, `tests/contract`, `tests/integration` and `tests/client` filters.
5. Demo/deploy the MVP if ready.

### Incremental Delivery

1. Setup + Foundational → shared detection/eligibility/wire id ready.
2. US1 → docx/pdf/text MVP.
3. US2 → real spreadsheets.
4. US3 → any answer, including History, can be saved.
5. US4 → trust/privacy/error hardening across all paths.
6. Polish → documentation, formatting, full gates and the observational SC checks.

### Notes

- `[P]` means different files and no incomplete dependency; do not parallelize tasks that edit the same file.
- Tests are mandatory under Constitution VI; a task that only changes production code without its failing test is incomplete.
- Every generated file is transient; never add it to `data/documents`, the library, LanceDB or a server cache.
- The from-scratch tier is out of scope; do not add an ungrounded generation path.

## Phase 8: Convergence

- [x] T083 Harden `DocumentTableBuilder.TryParseNumber` to parse parenthesised negatives (for example `(1,234.50)` and `(15%)`) and European `1.234,56` amounts without misreading them, and add cases to `tests/unit/DocumentTableBuilderTests.cs` and `tests/unit/XlsxRendererTests.cs` per FR-010 (partial).

