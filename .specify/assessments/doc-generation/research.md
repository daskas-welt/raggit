# Idea Research: Document Generation from Library Material or From Scratch

- **Slug**: doc-generation
- **Created**: 2026-11-16
- **Evidence confidence (overall)**: low

> Overall confidence remains low. The clarified request states that "from scratch" means generating Word, Excel, TXT and PDF documents **without relying on library documents**, and that demand is company-wide. A later developer-supplied estimate adds a rollout range, a rough volume and a time-per-document figure, but it is a projection rather than observed behaviour, and its provenance is internally contradictory. No ticket, employee interview, usage data or measured volume supports it. Internal constraints and prior art remain high-confidence and well cited.

## Users & Demand

- The requester — the same person behind `specs/033-ask-document-generation` — states that documents must be generatable "from scratch, without relying on existing library documents," in Word, Excel, TXT and PDF, and describes the demand as company-wide — [source: `.specify/assessments/doc-generation/intake.md` (interactive clarification)] (confidence: medium that it was stated; low that actual demand is company-wide)
- "Existing documents" was clarified to mean **library documents** only — [source: `.specify/assessments/doc-generation/intake.md`] (confidence: high)
- The prior request for document output came from the same requester and became `specs/033-ask-document-generation`, created 2026-11-01 and committed 2026-11-12 (`d19c695`), status **Draft** with no plan, tasks, or code — [source: `specs/033-ask-document-generation/spec.md:9`; `git log`; `.specify/feature.json`] (confidence: high)
- No open GitHub issue, support ticket, interview, usage data or analytics request exists for document generation; the repository has only three closed issues, none related — [source: `gh issue list --repo daskas-welt/raggit --state all`; repository tree] (confidence: high that no such evidence exists)
- A developer-supplied estimate gives a rollout range of 1–100 employees, "maybe 100 documents per day, depending on company size," ~25 minutes per document, and a branch split of "mostly library-derived; from-scratch for new policy drafts" — [source: user-supplied in chat, 2026-11-16] (confidence: low — a projection, not observed demand)
- The estimate is internally inconsistent on provenance: it says it is "driven by the developer" while also claiming a "direct interview" with frequency "estimated from last month's files"; no interview notes, files or measurements were provided — [source: user-supplied in chat, 2026-11-16] (confidence: high that the contradiction exists)
- The denominator for "100 documents per day" is unresolved. Across 100 employees that is about one document per employee per day; for a one-person company it would be 100 per person per day, which is implausible. The estimate is only coherent as a company-level figure. [NEEDS CLARIFICATION: per company or per employee]
- No open GitHub issue, support ticket, employee interview, usage data or analytics request exists for document generation; the repository has only three closed issues, none related — [source: `gh issue list --repo daskas-welt/raggit --state all`; repository tree] (confidence: high that no such evidence exists)

### Demand evidence supplied (2026-11-16)

| Claim | Value | Classification |
|-------|-------|----------------|
| Source | "It is driven by the developer" | Developer projection, not an employee interview |
| Population | "company-wide rollout planned for 1 to 100 employees" | Planned deployment range; current user count unknown |
| Frequency | "Maybe 100 documents per day, it depends on the company size" | Estimate; denominator and basis unknown |
| Current time | "~25 min per document" | Estimate; measurement method and sample unstated |
| Branch mix | "mostly library-derived; from-scratch for new policy drafts" | Useful qualitative split; no volumes attached |
| Provenance | "direct interview, frequency estimated from last month's files" | Contradicts "driven by the developer"; no records supplied |

This is recorded as a **developer projection**, not demand evidence. It is useful for scale intuition and for confirming that the library-derived branch is expected to dominate, but it does not show that employees currently feel the problem, how many actually do, or that the 25-minute figure is real.

## Prior Art

- Internal: 033 specifies Word, Excel, PDF and plain-text output from a **grounded** Ask answer, requiring content to match the displayed answer, carry citations, and return `no relevant content found` with no file when nothing matches; it excludes templates, branding, multi-sheet structures and library publication — [source: `specs/033-ask-document-generation/spec.md:106-126,170-178`] (confidence: high)
- Internal: 033 has **no** from-scratch branch and does not contemplate generation without library documents; the clarified request therefore adds a case 033 explicitly does not cover — [source: `specs/033-ask-document-generation/spec.md:168-178`] (confidence: high)
- Internal: no export or document-assembly code exists; the solution has 10 projects, none for export, and no save-dialog/export path was found — [source: `dotnet sln RAGGit.sln list`; search over `src/`] (confidence: high)
- Internal: `DocumentFormat.OpenXml` 3.5.1 and `PdfPig` 0.1.16 are already dependencies of `RAGGit.Ingest`, but only for reading uploads; PdfPig cannot create PDFs — [source: `src/RAGGit.Ingest/RAGGit.Ingest.csproj`; `Chunker.cs`; `DocumentFormatValidator.cs`] (confidence: high)
- External: document assembly is a mature library problem — `python-docx`, `docx4j`, `docx` (npm), `docxtemplater` for Word; `QuestPDF` (14,222 stars) for PDF generation in .NET — [source: GitHub repository search via `gh search repos docx` and `document-generation`; host `github.com`, policy: allowlisted; search results only, pages not fetched] (confidence: medium)
- External: general-purpose assistants (Microsoft 365 Copilot in Word, ChatGPT/Claude file outputs) already generate documents from prompts and attached files, so from-scratch generation is a commodity capability rather than a differentiator — [source: ASSUMPTION, not verified in this session] (confidence: low)
- The prior attempt (033) has not been built, so there is no internal evidence either way on whether document output succeeds here — [source: `specs/033-ask-document-generation/`] (confidence: high)

## Market & Context

- For library-derived documents, the alternative today is manual copy from the Ask view into an office suite, which loses citations and invites transcription error — [source: ASSUMPTION inferred from 033's acceptance scenarios and `problem.md`] (confidence: medium)
- For from-scratch documents, the alternative is drafting in an office suite or using an external AI assistant; the latter breaks the private/offline positioning — [source: ASSUMPTION] (confidence: medium)
- The product's stated differentiator is private, on-premises, **cited** answers — [source: `README.md:3-18`] (confidence: high for the positioning statement)
- The cost of inaction (time per document, error rate, volume) is not measured anywhere — [source: ASSUMPTION] (confidence: low)

## Data & Constraints

- **Grounding invariant**: every answer generated from the library must carry `documentId` + `chunkId` citations; with no relevant chunks the response must be exactly `no relevant content found` and must not hallucinate — [source: `.specify/memory/constitution.md:33`] (confidence: high)
- A document generated "from scratch, without relying on library documents" has no chunks to cite, so it falls outside the behaviour the grounding invariant defines for answers — [source: `.specify/memory/constitution.md:33`; `.specify/assessments/doc-generation/intake.md`] (confidence: high)
- **Offline invariant**: no query-time cloud egress; models and embeddings bundled or cached on the workstation; a missing model fails fast — [source: `.specify/memory/constitution.md:29`] (confidence: high)
- **Model headroom**: `qwen3:4b-instruct-2507` was the only measured chat model that never stated a false fact on the EY Greek corpus, but broad prompts take 5–11 minutes, hence `Ollama:TimeoutMs` 900000; smaller models are faster and demonstrably wrong — [source: `AGENTS.md:59-64`] (confidence: high)
- **Context budget**: `Ollama:NumCtx` 32768; a five-parent broad prompt is already ~19.5k tokens; truncation causes invention — [source: `AGENTS.md:52-58`] (confidence: high)
- **Corpus scale measured**: EY library 43 chunks, ebook library 2933 chunks; granular queries ~90 s — [source: `AGENTS.md:65-68`] (confidence: high)
- **Inputs are uploaded files only**: PDF, DOCX, XLSX and text/markdown, capped at 100 MB; there is no path for arbitrary local files or generated files to become inputs — [source: `AGENTS.md:72-75`; `src/RAGGit.Ingest/DocumentFormatValidator.cs`; 033 FR-014] (confidence: high)
- **Generated documents are not retained** under 033: they exist only where the employee saves them and never enter the shared library — [source: `specs/033-ask-document-generation/spec.md:125,170-172`] (confidence: high)
- **Formats named by the requester**: Word, Excel, TXT, PDF — [source: `.specify/assessments/doc-generation/intake.md`] (confidence: high that these were named; details of layout and data types unstated)
- **Single-tenant and per-user access**: one installation per company; employees see only what they are allowed to see — [source: `README.md:12-18`; `.specify/memory/constitution.md:17`] (confidence: high)

## Evidence Against the Idea

- The from-scratch branch, as clarified, produces business documents with no citation trail, which conflicts with the constitution's requirement that generated content cite chunks or refuse; supporting it requires an explicit policy change or a separately governed artifact class — [source: `.specify/memory/constitution.md:33`; `.specify/assessments/doc-generation/intake.md`] (confidence: high)
- Company-wide demand rests on a requester claim plus a developer projection, with no observed behaviour and no verified volume, frequency or current-workaround data; the provenance of the "direct interview / last month's files" claim is unresolved — [source: `.specify/assessments/doc-generation/intake.md`; user-supplied estimate 2026-11-16] (confidence: high)
- The measured local model is slow (5–11 min on broad prompts) and smaller alternatives are documented as factually wrong; long-form ungrounded composition is unproven and may exceed the 900 s timeout or 32k context — [source: `AGENTS.md:52-64`] (confidence: high)
- 033 already specifies the grounded half and remains unplanned; a second feature risks duplicating scope, splitting the decision, or contradicting 033's citation and refusal rules — [source: `specs/033-ask-document-generation/spec.md`; `.specify/feature.json`] (confidence: high)
- From-scratch document generation overlaps commodity general assistants; if the product builds it, it competes on a capability where its privacy/citation advantage does not apply — [source: ASSUMPTION] (confidence: low)
- No quality bar exists for an ungrounded document: there are no sources to check against, so 033's sampled-fact metrics cannot be reused — [source: `specs/033-ask-document-generation/spec.md:157-158`] (confidence: high)
- Making spreadsheets behave like data and PDFs open cleanly is real format work; the existing PDF dependency is read-only, so PDF output adds a new dependency and failure surface — [source: `src/RAGGit.Ingest/RAGGit.Ingest.csproj`] (confidence: high)

## Gaps & Open Questions

- [NEEDS CLARIFICATION: Who is the requester (name/role), and what specifically prompted adding the from-scratch branch?]
- [NEEDS CLARIFICATION: For a from-scratch document, where may content and facts come from if no library documents are used — the model's own knowledge, the employee's brief, or both?]
- [NEEDS CLARIFICATION: What input drives a from-scratch document — a free-text brief, a form, a template, or something else?]
- [NEEDS CLARIFICATION: How should the citation/refusal rule apply to a document with no sources, and how should it be labelled to avoid implying library support?]
- [NEEDS CLARIFICATION: How will a from-scratch document's accuracy or quality be judged, and by whom?]
- [NEEDS CLARIFICATION: Should generated documents be saved only locally (033's model), or may they enter the library and later serve as "existing documents"?]
- [NEEDS CLARIFICATION: Which roles and teams make up the company-wide demand, how often, and what do they do today? Is the 100-documents-per-day figure per company or per employee, and what files or interviews produced it?]
- [NEEDS CLARIFICATION: Does this extend/revise 033 or become a separate feature?]
- [NEEDS CLARIFICATION: What exact output details are expected for Word, Excel, TXT and PDF (layout, tables, data types), and are other formats in scope?]
- [NEEDS CLARIFICATION: How is the output language chosen — request language, user choice, or source language?]
- [NEEDS CLARIFICATION: What size, duration and volume limits apply, given the measured model latency and context budget?]
- [NEEDS CLARIFICATION: Are templates, branding, multi-document inputs and batch generation in scope?]

## Sources

- `.specify/assessments/doc-generation/intake.md` — refined idea, clarified branches, stated demand
- `specs/033-ask-document-generation/spec.md` — prior internal feature specification (grounded only)
- `specs/033-ask-document-generation/checklists/requirements.md` — 033 scope decisions
- `.specify/memory/constitution.md` — grounding, offline, single-tenant invariants
- `AGENTS.md` — measured model, context, corpus and upload constraints
- `README.md` — product positioning and user model
- `src/RAGGit.Retrieval/GenerationService.cs`, `src/RAGGit.Retrieval/QueryPipeline.cs` — grounded generation path
- `src/RAGGit.Ingest/RAGGit.Ingest.csproj`, `Chunker.cs`, `DocumentFormatValidator.cs` — existing document libraries and read-only usage
- `https://github.com/dolanmiu/docx`, `https://github.com/plutext/docx4j`, `https://github.com/python-openxml/python-docx`, `https://github.com/open-xml-templating/docxtemplater`, `https://github.com/QuestPDF/QuestPDF` (host: `github.com`, policy: allowlisted; discovered via `gh search repos`, pages not fetched)
- No external web page was fetched; no URL was refused.
