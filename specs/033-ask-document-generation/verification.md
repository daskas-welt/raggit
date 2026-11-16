# Verification: Ask-View Document Output

**Feature**: `033-ask-document-generation` | **Date**: 2026-11-16

## Automated gate (2026-11-16; re-run 2026-11-17 after the Ask startup-restore fix)

| Gate | Result |
|------|--------|
| `dotnet build RAGGit.sln -c Release -p:Platform=x64` | PASS |
| Unit tests | 650 passed |
| Contract tests | 110 passed |
| Client screen/XAML tests | 105 passed |
| Integration tests | 82 passed |
| Offline subset | 12/12 passed |
| `dotnet csharpier check .` | PASS |

The full integration run uses deterministic fakes where Ollama is absent; `RequiresOllama` tests are skipped by their normal guard.

## Requirement review

- **FR-001…FR-009**: four formats, wording-based detection, persisted-answer rendering, title/sources, language, filename, no-format/no-file, spreadsheet header/records and empty absent values are implemented and covered by unit/contract/integration tests.
- **FR-010**: documented date/amount formats are typed; unrecognized values stay text without guessing (unit tests).
- **FR-011…FR-015**: prose section fallback, save/open, OS dialog/no-partial write, no library retention, history behavior.
- **FR-016/FR-018**: only a cited, non-refusal answer is eligible; refusals and citationless answers expose no save action and return `422`.
- **FR-017/FR-019/FR-020**: the document is a rendering of the persisted answer; every source is a persisted citation; the renderer never retrieves or adds another record.
- **FR-021**: rendering is workstation-side, offline, with no new runtime egress; the client only calls the endpoint and saves through the dialog.
- **FR-022…FR-025**: user-safe failure messages, busy/one-at-a-time behavior, unsupported-format explanation, and the 25 MiB / 5 000-row cap with `422`.
- **From-scratch generation** remains out of scope per `.specify/assessments/doc-generation/decision.md`.

## Success-criteria evidence

| Criterion | Evidence | Status |
|-----------|----------|--------|
| SC-001 | Format render/open tests; 30/30 live renders parsed without repair (T078 sample: 4 distinct answers × 4 formats + repeats) | PASS (sample; 20-request target not reached) |
| SC-002 | Refusal/unsupported contract tests | PASS (automated) |
| SC-003 | Source-integrity tests; 30-document live audit, 0 content failures, 100% cited chunk ids present | PASS |
| SC-004 | Trust/source and cross-user isolation tests | PASS (automated); 20-person live sample blocked (single-person corpus) |
| SC-005 | Save-as tests; 30/30 live renders match the persisted answer and sources | PASS (sample; 20-answer target not reached) |
| SC-006 | Document render 23–643 ms vs ask 287,644–649,189 ms (model-free route) | PASS (<1% overhead) |
| SC-007 | 4/5 employees unaided | NOT MEASURED — requires five employees |
| SC-008 | No content-store/library writes (contract privacy test + live before/after count) | PASS |
| SC-009 | Offline subset 12/12 | PASS |
| SC-010 | Cancelled/failed/unsupported tests + live dialog cancel produced no file | PASS (automated/scripted) |

## Manual verification (2026-11-17)

Environment: live Debug workstation on `https://localhost:5001` (16 documents, the
single-person `Daskalopoulos_Andreas_*` corpus), Ollama `qwen3:4b-instruct-2507-q4_K_M`,
WPF client Release build. Requests were driven through the API and the running client
(`winapp ui`).

### T081 quickstart

| Scenario | Result |
|----------|--------|
| 1. Sign in and open Ask | PASS — the Ask page opened with the restored conversation (the separate startup-restore fix). |
| 2. Ask a format question | PASS via `POST /api/queries` — `Create a Word document with the phone number of Andreas Daskalopoulos` answered with 1 citation, `queryId 97a16243-…`, 345,560 ms. |
| 3. Answer/citations, save dialog, cancel, save, title/sources | PASS for the History/detail path: Save as → Word opened the OS dialog; saving produced `ui-save.docx` (3,005 bytes) whose title, answer and sources appendix match the answer; Save as → PDF → Cancel created no file. Word is not installed; the package was validated as OOXML and the renderer tests read it with OpenXml. |
| 4. Excel/PDF/text | PASS via the endpoint — all four formats rendered for the 4 persisted answers (16 distinct renders + 14 repeats); DOCX/XLSX read with OpenXml, PDF text extracted with pypdf, TXT as UTF-8. |
| 5. No-format answer saved in all four formats | PASS — the 3 prose answers were rendered in all four formats; the client Save as UI is covered by `DocumentSaveUiTests` / `QueryViewModelSaveAsTests`. |
| 6. History reopen + Save as | PASS — manual UI run above (Query Detail → Save as → Word). |
| 7. Refusal | Automated contract coverage PASS (`422 no document available`); the live refusal ask (`What is the capital of France?`) was cancelled after 20+ minutes without an answer, so no live refusal document was observed. |
| 8. Unsupported format (PowerPoint/image) | Automated detection and ViewModel tests PASS; not re-asked live. |
| 9. Library unchanged | PASS — 16 documents in the API and 16 files in `data/documents` before and after; generated files went only to the chosen OS paths. |
| 10. Greek/English headings | Automated `TextScriptTests` and renderer tests PASS; live answers were English. |

### T076 SC-003 trust audit

30 documents were generated: 4 distinct persisted answers × 4 formats (16), the 3 older
answers repeated in all four formats (12), and 2 repeats of the new answer (2). Every
render was checked against the persisted answer and its citations:

| Answers | Formats | Renders | Content failures | Cited chunk ids present | Unsupported statements |
|---------|---------|---------|------------------|--------------------------|------------------------|
| c00c8c73 (phone), fe4165ed (phone list), 9839dc1b (phone list), 97a16243 (format request) | docx, xlsx, pdf, txt | 30 | 0 | 30/30 | 0 |

Statement check: `6946712046` is verbatim in the CV chunk, `2610426086` in the EFKA
registration certificate chunk, and `2610455811` in the Cosmote bill chunk. The phrase
"ID document" in one answer refers to the EFKA registration certificate that carries the
ID-card details; the number itself is supported by the cited chunk. The spreadsheet was
also checked cell-by-cell (sheet1 content rows, sheet2 source rows with document,
ordinal, chunk id and excerpt).

### T078 SC-001/SC-005 sample

- Live requests: **5** (3 existing + the format request + the cancelled refusal), of
  which **4** are document-eligible.
- Answer saves: **30** renders across the four formats (4 answers × 4 formats + repeats);
  30/30 parsed without repair and 30/30 matched the persisted answer and sources.
- The 20-request / 20-answer targets were not reached: each ask takes 287–649 s on the
  reference workstation and the live corpus holds 4 persisted answers.

### T075 SC-006 / SC-007 / SC-010

- **SC-006**: the document route makes no model call; measured render time was 27–212 ms
  (DOCX), 32–37 ms (XLSX), 320–643 ms (PDF) and 23–28 ms (TXT) against ask latencies of
  287,644–649,189 ms. The added time is <1%, well inside the 15% bound. The 10-text vs
  10-document pair was not run; the model-free contract and code make the comparison
  structural.
- **SC-007**: NOT MEASURED — needs five employees who have not used the feature.
- **SC-010**: automated cancelled/failed/unsupported tests PASS; the live save-dialog
  cancel produced no file.

### T077 SC-004

NOT MEASURED — the live corpus is one person (16 Daskalopoulos documents), so 20
same-template documents about different people cannot be produced here. The invariant is
covered by the automated trust/source and cross-user isolation tests.

## Open verification work

- T075: SC-007 (five employees) remains unmeasured; SC-006 and SC-010 are recorded above.
- T077: the 20-person same-template audit needs a multi-person corpus.
- T078: 20 live requests/answers were not reached; the 30-render sample is recorded above.
- T081: the live refusal ask and unsupported-format UI were not observed; both paths have
  automated coverage.
