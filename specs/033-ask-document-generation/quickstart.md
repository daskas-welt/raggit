# Quickstart: Ask-View Document Output

**Feature**: `033-ask-document-generation` | **Date**: 2026-11-16

This guide proves the feature end to end. Automated tests are the primary gate; the manual scenario shows the employee experience.

## Prerequisites

- .NET 10 SDK (pinned by `global.json`).
- Ollama running with the configured chat and embedding models (the dev defaults). `RequiresOllama` tests skip when Ollama is absent.
- A library with at least one indexed document that covers a real subject (the EY Greek corpus or any single-column PDF/DOCX works).
- WPF client built for Windows; its `appsettings.json` has both `Workstation:Url` and `Workstation:ApiKey`.
- No from-scratch generation: this feature only documents answers grounded in the library.

## Build and format

```powershell
dotnet tool restore
dotnet build RAGGit.sln -c Release -p:Platform=x64
dotnet csharpier check .
```

Linux/API-only build:

```powershell
dotnet build RAGGit.Server.slnf -c Release
```

## Run the workstation and client

```powershell
# Terminal 1 — workstation
dotnet run --project src/RAGGit.Workstation.Api --urls https://localhost:5001

# Terminal 2 — desktop client (after appsettings.json is configured)
dotnet run --project src/RAGGit.Client.WPF
```

Create a local login if needed (run from `src/RAGGit.Workstation.Api` so the relative DB path resolves):

```powershell
.\bin\Release\net10.0\RAGGit.Workstation.Api.exe user add --username me --display-name "Me" --role Admin --password "Passw0rd!"
```

## Manual validation

1. Sign in and open **Ask**.
2. Ask a question that names a format, for example:
   `Create a Word document with the employment history of Μαρία Παπαδοπούλου.`
3. Confirm:
   - the answer appears with its citations, exactly as a normal answer;
   - the OS save dialog opens with a suggested filename;
   - cancelling leaves no file and no change to the conversation;
   - saving produces a `.docx` that opens in Word and matches the answer;
   - the document has a title, the answer text, and a sources appendix with document names, ordinals and chunk ids.
4. Repeat with `Excel` (or `spreadsheet`), `PDF`, and `text`/`txt`.
   - The spreadsheet has a header row, one row per record when the answer has repeated `label: value` blocks, and empty cells where the library holds no value.
   - Dates and amounts are recognized as dates/numbers in Excel.
   - The PDF opens in a standard reader and shows Greek text correctly.
5. Ask a question with no format, then use **Save as** on the answer and choose each format. The saved file matches the answer.
6. Open **History**, reopen an answer, and use **Save as** there. The file matches the displayed answer.
7. Ask something the library does not cover. The answer is exactly `no relevant content found`; no save action is offered.
8. Ask for a PowerPoint or an image. The answer still appears; an InfoBar lists Word, Excel, PDF and text; no file is produced.
9. Confirm the library's document list is unchanged after saving generated files.
10. Ask in Greek and confirm the document headings/labels are Greek; ask in English and confirm they are English.

## Automated validation

Run the smallest relevant suites first, then the full gate.

```powershell
# Unit — detection, table shaping, renderers, filenames, ViewModel lifecycle
dotnet test tests/unit/RAGGit.Tests.Unit.csproj -c Release --filter "FullyQualifiedName~Document"

# Contract — endpoint auth/ownership/format/content type/filename/422/queryId
dotnet test tests/contract/RAGGit.Tests.Contract.csproj -c Release --filter "FullyQualifiedName~Document"

# Integration — ask → render → read the file back; refusal; cross-user isolation
dotnet test tests/integration/RAGGit.Tests.Integration.csproj -c Release --filter "FullyQualifiedName~Document"

# Client — every screen, new XAML, bindings and WPF-UI traps
dotnet test tests/client/RAGGit.Tests.Client.csproj -c Release

# Full solution gate
dotnet test RAGGit.sln -c Release -p:Platform=x64
dotnet csharpier check .
```

Expected:
- Unit tests read DOCX/XLSX back with OpenXml, PDF with PdfPig, and TXT as UTF-8; every cited chunk id appears in the sources appendix.
- Contract tests prove `404` for another user's query, `422` for a refusal or a citationless answer, `400` for an unknown format, and the correct `Content-Type`/`Content-Disposition` for each format.
- Integration tests prove the answer row is unchanged, no generated file reaches `data/documents` or LanceDB, and the offline subset (`QueryOfflineTests|OfflineIdentityTests|OfflineHistoryTests|CrossUserIsolationTests`) stays green.
- Client tests construct the Ask page and chat control with the new save UI and report no XAML or binding errors.

## Measurement checks (033 SC-001, SC-003, SC-004, SC-005, SC-006, SC-007, SC-010)

- **SC-001/SC-003/SC-004/SC-005**: run the sampled audits recorded in `verification.md`. SC-003 uses 30 documents checked against their sources, SC-004 uses 20 same-template documents, and SC-001/SC-005 use 20-request/20-answer samples. Record every failure; the target is zero.
- **SC-006**: on the same workstation with the model already loaded, ask 10 questions as text only and then the same 10 as documents; the document route must add no more than 15% to the elapsed time. Record the timings in the feature's verification notes.
- **SC-007**: with five employees who have not used the feature, at least four must produce and save a document on their first attempt without assistance.
- **SC-010**: run the scripted cancelled-save, failed-generation and unsupported-format cases and confirm each leaves zero files and shows one clear message.

## Manual test data notes

- A `Ready` document must be deleted and re-uploaded to pick up extraction/chunking changes; this feature changes neither.
- Test projects use `tests/<suite>/bin/<Config>/net10.0/data/`; wipe that output directory if stale state breaks a run.
- A live API or open Visual Studio can lock `bin/Debug`; use `-c Release --no-build` for repeat runs.

## Troubleshooting

| Symptom | Cause / action |
|---------|----------------|
| `model unavailable offline` | Ollama/model missing; the answer cannot be produced, so no document either. |
| `404` on a document request | The query is not owned, missing, or legacy; reopen it from History. |
| `422 no document available` | The answer is a refusal or has no citations; no file is expected. |
| `422 document too large` | Narrow the request; the 25 MiB / 5 000-row cap was hit. |
| PDF shows boxes instead of Greek | The embedded DejaVu font asset is missing from the build; rebuild `RAGGit.Documents`. |
| Save dialog never appears | The client is not on the UI thread or `IFileSaveService` is not registered; check DI. |
| `dotnet csharpier check .` fails | Run `dotnet csharpier format .` after touching `.xaml`. |
