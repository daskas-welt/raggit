# Concept: Library-First Document Generation, With From-Scratch Deferred

- **Slug**: doc-generation
- **Created**: 2026-11-16
- **Recommended option**: Option B — Library tier first, from-scratch deferred

## Options

### Option A — Do nothing / keep using office tools and outside assistants

- **Sketch**: Build nothing. Employees copy grounded answers into Word or re-key values into Excel themselves; when they need a document with no library material, they draft it in an office suite or with an external AI assistant. The product stays a question-and-answer surface, and `specs/033-ask-document-generation` remains a Draft.
- **Appetite**: small (no build; support and documentation only).
- **Trade-offs**: Wins cost, schedule and risk: no new surface, no policy change, no model-quality exposure. Sacrifices both branches of the request. Risk: the projected company-wide demand stays unmet, and employees wanting from-scratch drafts may use external AI, which breaks the private/offline positioning.
- **Rabbit holes**: none to build; the hidden cost is repeated manual effort and a widening gap between what the product answers and what employees deliver.

### Option B — Library tier first, from-scratch deferred

- **Sketch**: Ship the library-derived branch as the first increment: an employee asks for a document and receives a finished Word, Excel, TXT or PDF file whose content is the grounded answer already shown, with the same sources. If the library has nothing relevant, the answer stays exactly `no relevant content found` and no file is offered. The from-scratch branch is explicitly deferred; the product does not yet produce unsourced content. This is `specs/033-ask-document-generation`, treated as the response to the dominant, better-understood part of the request.
- **Appetite**: medium (weeks).
- **Trade-offs**: Wins the trust argument completely: every statement remains traceable, the citation and refusal invariants are untouched, and the private boundary is preserved. Reuses an existing Draft rather than inventing a second feature, and matches the only demand signal available — the requester's projection that documents will be "mostly library-derived; from-scratch for new policy drafts". Sacrifices the from-scratch branch, so employees drafting new policy still have no supported route. Risks: document-length output on the measured local model may be slow or exceed the context/time budget; format fidelity is real work; usefulness is bounded by library coverage; and the projected demand is still a developer estimate.
- **Rabbit holes**: table-versus-prose shape for spreadsheets; preserving citations and source lists across four formats; long outputs exceeding the 32k context or 900 s timeout; filename/save/cancel/overwrite semantics; the temptation to add templates, branding or editing once a document exists; keeping the document a rendering of the shown answer rather than a second, divergent generation.

### Option C — Two tiers together

- **Sketch**: The employee chooses, in ordinary language, whether the document should be built from the library or started from scratch. The library tier behaves like Option B. The from-scratch tier takes a brief from the employee and composes a new document; it carries no citations, is visibly marked as **not checked against the library**, and never presents itself as library-supported. Provenance is visible in the conversation, history and the saved file. Both tiers save locally; neither publishes to the shared library.
- **Appetite**: large (months).
- **Trade-offs**: Wins the whole clarified request in one release: both branches are served and the projected policy-draft need has a path. Preserves privacy by keeping composition on the workstation. Sacrifices the current blanket "everything is cited" story: the product would produce unsourced business documents, so the distinction must be unmissable and the citation/refusal policy must explicitly cover them. Risks: unsourced facts in a business document; liability and reputational exposure; a quality bar that cannot be checked against sources; the measured model's long-form speed and accuracy are unproven; and the company-wide volume is a projection. The from-scratch tier is also close to commodity assistants, where the citation advantage does not apply.
- **Rabbit holes**: defining quality with no sources; preventing invented facts about real people or records; making provenance visible and consistent across conversation, history and file; deciding where spreadsheet values come from when there is no library; templates and fill-in structures; policy approval and training; long-form limits; scope creep into authoring, editing and approval.

## Recommendation

**Option B — Library tier first, from-scratch deferred.** The new baseline changes the balance: the only demand signal says most documents will be library-derived, and from-scratch is reserved for new policy drafts. Option B serves that dominant case with a defined trust model, reuses 033, and delivers in weeks rather than months. It satisfies goals 1, 3 (for library-backed documents), 4 and 5 and the measurable outcomes already written in 033. It does not satisfy goal 2.

The from-scratch branch should be treated as the next decision, not abandoned: if demand and a quality bar are established, Option C's from-scratch tier can be added as a separate increment without reopening the library tier. If the requester will not accept a library-only first release, Option C is the only option that covers the clarified request, and it must first settle the provenance policy and the quality bar. If neither the demand projection nor the requester's commitment survives scrutiny, Option A becomes defensible.

## Out of Scope (for the recommended option)

- Generating documents with no library grounding, or any from-scratch prose.
- Using library records inside an unsourced tier (the tier itself is deferred).
- Adding generated documents to the shared library, or retaining them after delivery.
- Templates, branding, multi-sheet or multi-document structures.
- In-app editing, collaboration, versioning, approval, signing, scheduling, batch generation or mail merge.
- Reading arbitrary local files, or files outside the indexed library, as inputs.
- Formats beyond Word, Excel, TXT and PDF.
- Multi-tenant or cross-company use, and any external AI or document service.
- Matching general-purpose AI assistants feature-for-feature.
- Automated legal, compliance or management review.

## Assumptions to Validate

- The projection that documents will be "mostly library-derived" is directionally right; it is the only demand signal available and is a developer estimate, not observed behaviour. [source: `.specify/assessments/doc-generation/research.md`]
- The requester accepts a library-only first release and treats from-scratch as a follow-on rather than a launch requirement. [NEEDS CLARIFICATION]
- `specs/033-ask-document-generation` can be carried forward as the library tier rather than replaced. [source: `specs/033-ask-document-generation/spec.md`]
- The local model can produce document-length grounded content within the existing time and context budget at acceptable quality; this is unproven at document length. [source: `AGENTS.md:52-68`]
- The four formats can be produced with acceptable fidelity on the workstation without external services or breaking the offline invariant.
- Employees have standard applications that open the produced formats without repair prompts. [source: `specs/033-ask-document-generation/spec.md:155`]
- No retention or sharing is required; the employee's own saved copy is the deliverable. [source: `specs/033-ask-document-generation/spec.md:125,170-172`]
- Company-wide demand exists at all; the projection has not been validated with employees. [NEEDS CLARIFICATION]
