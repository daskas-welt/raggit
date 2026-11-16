# Decision: Library-First Document Generation

- **Slug**: doc-generation
- **Decided**: 2026-11-16
- **Verdict**: go — library tier only, under explicit demand-risk acceptance
- **Artifacts reviewed**: intake.md | research.md | problem.md | concept.md

## Scorecard

| Criterion | Rating | Justification |
|-----------|--------|---------------|
| Problem validity | adequate | The library-derived problem is concrete: manual copy/re-key loses citations and invites error, and the requester projects that most volume is library-derived (`problem.md`). The from-scratch problem remains asserted rather than described. |
| Evidence strength | weak (risk accepted) | Demand is a developer projection (1–100 employees, "maybe 100 documents/day", ~25 min/document) with contradictory provenance; no employee interview, usage count or timed sample exists (`research.md`). This rating is **not** upgraded by the decision; the risk is accepted instead. |
| Value vs. inaction | adequate | Option B removes a real manual step for the dominant projected use and preserves the citation trail. The deferred from-scratch tier's value remains unmeasured (`problem.md`; `concept.md`). |
| Feasibility / appetite | adequate | Option B is a medium, weeks-scale increment that reuses the existing 033 Draft; the main risks are model latency, context limits and format fidelity (`concept.md`; `AGENTS.md:52-68`). |
| Strategic fit | strong | Option B keeps the citation, offline and single-tenant principles intact. From-scratch is deferred and would need a separate policy judgment (`constitution.md:17,29,33`). |
| Risk posture | adequate | Option B's product risks are bounded by 033's acceptance criteria and do not change the trust model. The residual demand risk is understood and now explicitly accepted by the decision-maker. |

## Verdict & Rationale

**Go — library tier only.** The decision-maker has explicitly accepted the demand risk and directed the library tier to proceed. This is a deliberate override of the evidence gate: evidence strength remains `weak` and is recorded as such, not re-rated. The go therefore rests on the decision-maker's risk acceptance, not on new demand evidence.

The approved scope is Option B: library-derived documents (Word, Excel, TXT, PDF) whose content is the grounded answer already shown, with citations preserved and `no relevant content found` still refusing to produce a file. The from-scratch branch is **not** approved; it remains parked until demand and a quality bar exist. The vehicle is the existing `specs/033-ask-document-generation`, not a new specification.

The accepted risk: the projected volume and value may not materialise, in which case the library tier is underused. Mitigation is to instrument adoption after release and stop before extending to from-scratch if usage is low. This should be revisited at the first post-release review.

## Risk Acceptance

- **Risk accepted**: building the library tier on a developer projection rather than observed demand.
- **Accepted by**: the decision-maker in this session, 2026-11-16.
- **Scope of acceptance**: the library tier only. The from-scratch branch is not covered and must not be inferred from this acceptance.
- **Mitigation**: measure usage after release (document requests, saved files, active employees), and treat low adoption as a signal to stop rather than expand.
- **Revisit**: first post-release review, or if a real demand signal changes the picture earlier.

## If go — Handoff to `/speckit.specify`

- **Problem**: Employees need finished Word, Excel, TXT and PDF documents; today library-derived documents are assembled by copying answers and re-keying values, losing citations and inviting error.
- **Chosen approach**: Option B — library-derived document output only, reused from `specs/033-ask-document-generation`; from-scratch explicitly deferred.
- **In scope / out of scope**: In — four formats from the grounded answer, citations and source list preserved, refusal unchanged, local save through the normal dialog, no retention, no library publication. Out — from-scratch/ungrounded documents, templates and branding, in-app editing, collaboration, approvals, batch generation, local-file inputs, formats beyond the four, multi-tenant, external services.
- **Success metrics**: 033's measurable outcomes (zero unsupported statements in a 30-document sample; zero cross-record contamination in 20; 100% open without repair in 20; 4/5 employees unaided; offline suite unchanged; zero generated files in the library), plus post-release adoption against the projection.
- **Carried-forward open questions**: exact format details (spreadsheet shape, PDF layout, data types); language rule; size/time/volume limits; whether 033 needs revision before planning; whether generated documents remain local-only; how adoption will be measured.
- **Next step**: `specs/033-ask-document-generation` already exists as a Draft. Update it if the clarified scope requires it, then run `/speckit.plan` on that feature — do **not** create a second specification for the library tier.
