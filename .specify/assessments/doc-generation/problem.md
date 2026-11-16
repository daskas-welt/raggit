# Problem Definition: Producing Finished Documents From Library Material or Without It

- **Slug**: doc-generation
- **Created**: 2026-11-16
- **Inputs used**: intake.md, research.md (including the 2026-11-16 developer projection)

## Problem Statement

Employees across the company need finished Word, Excel, TXT and PDF documents; the requester projects a rollout of 1–100 employees and roughly 100 documents per day, mostly library-derived, with from-scratch drafting for new policy. If that projection holds, library-derived documents are assembled by copying answers and re-keying values by hand — losing citations and inviting error — while new drafts with no library material have no supported route inside the private workstation and are produced in another tool or with an external AI service outside the company's boundary.

## Affected Users & Stakeholders

- **Users**: Employees who need a deliverable rather than a chat answer. The requester projects a company-wide rollout of 1–100 employees; the specific roles, teams and current document types are not identified. [source: `.specify/assessments/doc-generation/research.md` (2026-11-16 developer projection); NEEDS CLARIFICATION: which roles and how many currently]
- **Users**: Recipients and readers of the finished document — they rely on its facts, figures and names being correct, and need to know whether its content is supported by the library or composed without sources. [source: ASSUMPTION; `specs/033-ask-document-generation/spec.md:68-83`]
- **Stakeholders**: Administrators and IT — responsible for keeping the deployment private, single-tenant, and free of query-time external services; they bear the risk if documents are produced outside that boundary. [source: `README.md:12-18`; `.specify/memory/constitution.md:17,29`]
- **Stakeholders**: Compliance and audit — depend on the citation trail that makes a library-derived document defensible; a document with no sources cannot offer that trail. [source: `.specify/memory/constitution.md:33`; `specs/033-ask-document-generation/spec.md:68-83`]
- **Stakeholders**: The requester/developer — the same person behind 033, who supplied the rollout and volume projection and states the need is company-wide; identity, role and decision authority remain unknown. [source: `.specify/assessments/doc-generation/intake.md`; `research.md` demand evidence table; NEEDS CLARIFICATION: name and role]

## Goals

- An employee can produce a finished document from library material without manually copying text or re-keying values, and the document preserves the facts, names, figures and citations of that material.
- An employee can produce a new document without any library material, in Word, Excel, TXT or PDF, without leaving the company's private boundary.
- Every produced document makes clear whether its content is supported by library sources or composed without them; unsourced content is never presented as if it were sourced.
- The finished document is usable in the employee's normal tools — it can be opened, read, edited, printed and shared.
- Document production preserves the single-tenant, offline-at-query-time, per-user access posture of the product.

## Non-Goals

- In-app authoring, editing, collaboration, versioning, approval, signing, scheduling, batch generation or mail merge. [source: `specs/033-ask-document-generation/spec.md:173-177`]
- Replacing a document-management or storage system; generated documents are not a library by default. [source: `specs/033-ask-document-generation/spec.md:170-172`]
- Treating generated content as an independent source of truth about people, records or the company. [source: `.specify/memory/constitution.md:33`; `specs/033-ask-document-generation/spec.md:78-80`]
- Multi-tenant or cross-company document production. [source: `.specify/memory/constitution.md:17`]
- New external AI or document services, or query-time WAN access. [source: `.specify/memory/constitution.md:29`]
- Formats beyond the four named by the requester (Word, Excel, TXT, PDF). [source: `.specify/assessments/doc-generation/intake.md`]
- Matching general-purpose AI assistants feature-for-feature; the product's value is private, cited knowledge work, not open-ended chat. [source: `README.md:3-18`]
- Automated legal, compliance or management review of produced documents. [source: ASSUMPTION]

## Success Metrics

- Share of library-derived document requests completed without manual copy or re-key — baseline: 0% (no document-output path exists today). [source: `research.md`; `dotnet sln RAGGit.sln list`]
- Time from request to saved document, for both branches — baseline: ~25 minutes per document, a developer estimate that is not measured or attributed to a sample. [source: `research.md` demand evidence table; NEEDS CLARIFICATION: measurement method]
- Volume context: ~100 documents per day at company level, projected to depend on company size — baseline: unverified; the denominator (per company or per employee) is unresolved. [source: `research.md` demand evidence table]
- Zero statements or cell values in a library-derived document that are unsupported by the sources it lists (sample of 30 documents). [source: `specs/033-ask-document-generation/spec.md:157`]
- Zero documents containing content from another person's record when subjects share a template (sample of 20). [source: `specs/033-ask-document-generation/spec.md:158`]
- 100% of produced documents open without a repair prompt in the corresponding standard application (sample of 20). [source: `specs/033-ask-document-generation/spec.md:155`]
- At least four of five employees produce and save a document unaided on their first attempt. [source: `specs/033-ask-document-generation/spec.md:161`]
- Share of documents whose grounded/unsourced status is correctly understood by a sample of readers — baseline: unknown. [NEEDS CLARIFICATION: comprehension test and target]
- The offline verification suite passes unchanged, confirming no external access at production time. [source: `specs/033-ask-document-generation/spec.md:163`; `.specify/memory/constitution.md:29`]
- Zero generated files appear in the shared library after a generation session. [source: `specs/033-ask-document-generation/spec.md:162`]
- For no-library documents, an accuracy/acceptance signal — definition and target: unknown. [NEEDS CLARIFICATION: how quality is judged when there are no sources to check against]
- Company-wide adoption — baseline: no current users of document generation; target population is projected at 1–100 employees but not validated. [NEEDS CLARIFICATION: actual target and threshold]

## Cost of Inaction

The manual step persists for library-derived documents: employees keep copying grounded answers into their own tools, losing citations as the content leaves the app, and re-keying values by hand, which invites transcription error. At the projected 100 documents per day and 25 minutes each, the implied manual effort is roughly 42 person-hours per day, but neither figure is verified. Employees who need a document with no library material have no supported route inside the product and may draft it with an external or cloud AI service, which breaks the private, offline promise the product is sold on. The prior specification for grounded document output (`specs/033-ask-document-generation`, Draft) remains unbuilt, so the gap between "answer" and "deliverable" stays open. No observed time, volume, error or demand baseline exists. [source: `research.md`; `README.md:12-16`]

## Open Questions

- [NEEDS CLARIFICATION: Who is the requester (name/role), and what specifically prompted adding the from-scratch branch?]
- [NEEDS CLARIFICATION: Is the demand projection developer-driven or based on employee interviews? Which employees, files or dates support the "direct interview" and "last month's files" claims?]
- [NEEDS CLARIFICATION: Is "100 documents per day" per company or per employee, and which document types make up that volume?]
- [NEEDS CLARIFICATION: Which roles and teams make up the company-wide demand, and what do they do today?]
- [NEEDS CLARIFICATION: For a no-library document, where may content and facts come from — the model's own knowledge, the employee's brief, or both?]
- [NEEDS CLARIFICATION: What input drives a no-library document — a free-text brief, a form, a template, or something else?]
- [NEEDS CLARIFICATION: How should a no-library document's accuracy or quality be judged, and by whom?]
- [NEEDS CLARIFICATION: How should the citation/refusal rule and any "unsourced" labelling apply to a document with no library sources?]
- [NEEDS CLARIFICATION: Should no-library documents be saved only locally (033's model), or may they enter the library and later serve as "existing documents"?]
- [NEEDS CLARIFICATION: Does this extend/revise `specs/033-ask-document-generation` or become a separate feature?]
- [NEEDS CLARIFICATION: What exact output details are expected for Word, Excel, TXT and PDF (layout, tables, data types), and are other formats in scope?]
- [NEEDS CLARIFICATION: How is the output language chosen — request language, user choice, or source language?]
- [NEEDS CLARIFICATION: What size, duration and volume limits apply, given the measured model latency and context budget?]
- [NEEDS CLARIFICATION: Are templates, branding, multi-document inputs and batch generation in scope?]
- [NEEDS CLARIFICATION: May a no-library document mention real people or records at all, and if so, under what sourcing rule?]
- [NEEDS CLARIFICATION: Who reviews or accepts a produced document before it is used, if anyone?]
