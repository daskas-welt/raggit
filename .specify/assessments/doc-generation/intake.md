# Idea Intake: Document Generation from Library Material or From Scratch

- **Slug**: doc-generation
- **Created**: 2026-11-16
- **Source**: pasted text, refined interactively
- **Type**: new-capability

## Idea (as captured)

Original request:

> "User should be able to generate new documents based on existing documents or from scratch."

Clarified by the requester in this session:

> "The system must be able to generate new documents (in Word, Excel, TXT, and PDF formats) from scratch, without relying on existing library documents."

- "Existing documents" means **library documents** — documents already ingested into the shared library.
- Demand is described as **company-wide**.

Related context found in the repository (recorded, not evaluated):

- `specs/033-ask-document-generation/spec.md` ("Document Output from the Ask View", created 2026-11-01) specifies producing Word, Excel, PDF and text files from a grounded Ask answer. It is built on the library: content must match the displayed answer, carry citations, and a request that matches nothing returns `no relevant content found` with no file.
- The clarified idea is broader than 033: it adds generation that does not rely on library documents at all.
- `.specify/feature.json` points at `specs/033-ask-document-generation`, which is still Draft with no plan, tasks, or code.

## Restated

Users should be able to create new Word, Excel, TXT and PDF documents either from documents already in the company library or entirely without library material.

## Origin & Context

- **Raised by**: the same requester as `specs/033-ask-document-generation`. [NEEDS CLARIFICATION: requester's name and role]
- **Trigger**: [NEEDS CLARIFICATION: what prompted the request, and specifically what prompted adding the from-scratch branch]
- **Demand**: described as company-wide. [NEEDS CLARIFICATION: how many employees, how often, and what they do today]
- **Prior work**: 033 covers the grounded branch only and remains unplanned. [source: `specs/033-ask-document-generation/spec.md`; `git log`]

## First-Glance Unknowns

- [NEEDS CLARIFICATION: Who is the requester, and why is the from-scratch branch needed now?]
- [NEEDS CLARIFICATION: For a from-scratch document, where may its content and facts come from, given that no library documents are used?]
- [NEEDS CLARIFICATION: What input drives a from-scratch document — a free-text brief, a form, a template, or something else?]
- [NEEDS CLARIFICATION: How should the citation and "no relevant content found" rules apply to a document that has no library sources?]
- [NEEDS CLARIFICATION: How will the accuracy or quality of a from-scratch document be judged, and by whom?]
- [NEEDS CLARIFICATION: Should generated documents be saved only to the employee's machine (033's model), or may they be added to the library and later used as "existing documents"?]
- [NEEDS CLARIFICATION: What exact output types are expected for Word, Excel, TXT and PDF, and are any other formats in scope?]
- [NEEDS CLARIFICATION: Which roles or teams make up the "company-wide" demand, and how frequently would they generate documents?]
- [NEEDS CLARIFICATION: Does this extend or revise 033, or replace it with a new feature?]
- [NEEDS CLARIFICATION: How should the output language be chosen — the request's language, a user choice, or the source documents' language?]
- [NEEDS CLARIFICATION: What size, duration and volume limits apply, given the measured model latency and context budget?]
- [NEEDS CLARIFICATION: Are templates, branding, multi-document inputs and batch generation in scope?]
