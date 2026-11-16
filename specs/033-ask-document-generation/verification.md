# Verification: Ask-View Document Output

**Feature**: `033-ask-document-generation` | **Date**: 2026-11-16

## Automated gate (2026-11-16)

| Gate | Result |
|------|--------|
| `dotnet build RAGGit.sln -c Release -p:Platform=x64` | PASS |
| Unit tests | 647 passed |
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
| SC-001 | Format render/open tests; 20-request sample | Sample audit pending (T076–T078) |
| SC-002 | Refusal/unsupported contract tests | PASS (automated) |
| SC-003 | Source-integrity tests; 30-document sample | Sample audit pending (T076–T078) |
| SC-004 | Trust/source tests; 20 same-template sample | Sample audit pending (T076–T078) |
| SC-005 | Save-as tests; 20-answer sample | Sample audit pending (T076–T078) |
| SC-006 | ≤15% extra workstation time | Manual benchmark pending (T075) |
| SC-007 | 4/5 employees unaided | Manual usability check pending (T075) |
| SC-008 | No content-store/library writes (contract privacy test) | PASS (automated) |
| SC-009 | Offline subset 12/12 | PASS |
| SC-010 | Cancelled/failed/unsupported tests | PASS (automated/scripted) |

## Open verification work

- T075–T078: SC-006/SC-007 measurements and the 30/20/20 sampled audits.
- T081: manual quickstart run.
