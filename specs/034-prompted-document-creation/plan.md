# Implementation Plan: Description-Based Document Creation

**Branch**: `034-prompted-document-creation` | **Date**: 2026-11-17 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/034-prompted-document-creation/spec.md`

## Summary

Add a standalone flow where a signed-in employee describes a deliverable, chooses TXT/PDF/DOCX/XLSX, and saves the result locally. The workstation retrieves relevant library chunks using the existing retrieval policy. If none are relevant, a document-specific generation path still composes from the description and labels the output as not library-grounded. Library-derived facts must cite only retrieved chunks.

Keep feature 033's saved-answer export unchanged. Its decision record explicitly limited 033 to library-derived answer export; this is a separate, newly requested capability, not a scope change to 033. Reuse the existing retrieval/model stack, output formats, auth, and file-save service. Add no new project, external dependency, persistence, or WAN call.

## Technical Context

**Language/Version**: C# / .NET 10 (`global.json`: SDK 10.0.401).

**Primary Dependencies**: Existing ASP.NET Core MVC and `System.Text.Json`; `RAGGit.Core`, `RAGGit.Retrieval`, and `RAGGit.Documents`; `DocumentFormat.OpenXml` 3.5.1 and SkiaSharp 4.156.1 already used by the renderers; `CommunityToolkit.Mvvm` 8.2.2; WPF-UI 4.3.0. No new package is planned.

**Storage**: No new storage. The existing document library and vector store are read-only inputs. Request, draft, and rendered bytes exist only in memory and the response; the client writes only to a location chosen in the OS save dialog.

**Testing**: xUnit unit, contract, integration, and Windows client screen tests. Read generated DOCX/XLSX/PDF files back with existing OpenXml/PdfPig test dependencies. Run CSharpier for changed C# and XAML.

**Target Platform**: Windows WPF desktop client and .NET 10 workstation API; the server filter must continue to build on Linux. Company LAN/private network only; no WAN dependency at generation time.

**Project Type**: Existing multi-project desktop/API solution; no new project.

**Performance Goals**: At least 90% of valid requests produce a usable file or a clear, actionable failure within 15 minutes. The new typed creation client timeout must exceed the workstation's configured 900-second model timeout; do not change the Ask client timeout.

**Constraints**: One file per request; supported formats are exactly TXT, PDF, DOCX, XLSX. Use `Retrieval:TopK` and `Retrieval:MinScore` rather than adding retrieval settings. Reuse the existing 25 MiB document and 5,000-row workbook caps. Validate descriptions to 1–10,000 characters. Generated outputs are not library/history records. Preserve the no-WAN, thin-client, citation, single-tenant, and same-person-record invariants.

**Scale/Scope**: One API operation, one document-creation page, one client ViewModel/API client, transient request/content models, a renderer input generalization, API contract 1.8.0, and tests. No migration, background worker, persistent history, new project, or new configuration key.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Single-Tenant On-Prem or Private Cloud**: PASS — no tenant dimension or `company_id`; the existing company library remains the sole source, and same-person record isolation is a generation invariant.
- **II. Workstation-Owned AI**: PASS — retrieval and model generation remain on the workstation; the desktop sends the request, saves the returned bytes, and carries no AI or document-rendering library.
- **III. .NET Library-First & Client Reuse**: PASS — composition lives in `RAGGit.Retrieval`; shared request/content types and existing `RAGGit.Documents` rendering are reused; UI behavior stays in `Client.Core` and WPF.
- **IV. Offline Invariant (NON-NEGOTIABLE)**: PASS — retrieval and `ILlmClient` use the existing workstation stack; the feature adds no WAN call or runtime package/font fetch. Add an offline integration check.
- **V. Citation-Grounded RAG**: PASS with explicit boundary — library-derived factual claims must cite retrieved source chunks. Prompt-only output is allowed only when retrieval returns no relevant chunks and is labelled as not library-grounded. Existing `QueryPipeline` refusal (`no relevant content found`) and Ask behavior remain unchanged.
- **VI. Test-First (NON-NEGOTIABLE)**: PASS — add unit/contract/integration/client tests before implementation; verify every output format by reading the generated artifact; run the existing offline gate.
- **VII. Simplicity & Proprietary Stewardship**: PASS — no new project, package, persistent state, or configurable tuning. Existing private distribution and local model ownership are unchanged.

**Pre-Phase-0 gate**: PASS. The user's separate 034 request explicitly asks for description-based creation; the 033 decision remains limited to 033 and is not rewritten or broadened.

**Post-Phase-1 re-check**: PASS — the API contract, data model, and UI contract retain the thin-client/offline boundary, require source attribution for library-derived content, preserve 033, and add no database schema or runtime egress.

## Project Structure

### Documentation (this feature)

```text
specs/034-prompted-document-creation/
├── plan.md
├── research.md
├── data-model.md
├── contracts/
│   ├── api.yaml
│   └── ui-contracts.md
├── quickstart.md
├── spec.md
└── tasks.md                         # Phase 2 (/speckit.tasks), not created here
```

### Source Code (repository root)

```text
src/
├── RAGGit.Core/Models/
│   ├── DocumentCreationRequest.cs   # New wire request: description + format
│   └── DocumentDraft.cs             # New transient renderer input + citations
├── RAGGit.Retrieval/
│   ├── GenerationService.cs         # Add document-purpose prompt/generation path
│   └── DocumentCreationPipeline.cs  # Retrieve, compose, map only retrieved citations
├── RAGGit.Documents/
│   ├── DocumentGenerator.cs         # Add draft overload; retain QueryDetail eligibility
│   ├── DocumentNaming.cs            # Map draft citations; keep filename/localized labels
│   ├── DocumentTableBuilder.cs      # Preserve mixed content and shape usable XLSX sections
│   └── Renderers/{Docx,Xlsx,Pdf,Txt}Renderer.cs
├── RAGGit.Workstation.Api/
│   ├── Controllers/DocumentCreationController.cs # POST /api/documents/generate
│   └── Program.cs                   # Register creation pipeline
├── RAGGit.Client.Core/
│   ├── Services/DocumentCreationApiClient.cs
│   └── ViewModels/DocumentCreationViewModel.cs
└── RAGGit.Client.WPF/
    ├── App.xaml.cs                  # Register typed client, ViewModel, and page
    ├── ViewModels/MainWindowViewModel.cs # Add Create Document navigation item
    └── Views/Pages/DocumentCreationPage.xaml(.cs)

tests/
├── unit/                            # pipeline, renderers, API client, ViewModel
├── contract/                        # auth, validation, formats, errors, no persistence
├── integration/                     # source-backed, prompt-only, isolation, offline
└── client/
    ├── ScreenLoadTests.cs            # Include and construct the new page
    └── DocumentCreationUiTests.cs
```

**Structure Decision**: Keep the current project graph. `DocumentCreationPipeline` belongs in the existing retrieval/generation library; `DocumentDraft` belongs with shared Core models so Retrieval and Documents do not depend on each other. `RAGGit.Documents` accepts this neutral draft and renders it. Existing `DocumentGenerator.Generate(QueryDetail, ...)` retains its refusal/citation eligibility rule and adapts saved answers to the shared rendering input. The API controller handles validation, role authorization, response headers, and safe error mapping only. A separate creation API client keeps its long generation timeout and error vocabulary isolated from the 033 export client; both reuse the existing `DocumentFile` response type and `IFileSaveService`.

## Complexity Tracking

No constitution exceptions require justification. The design adds no project, dependency, configuration key, database table, or retained generated-document state.
