# Feature Specification: Intent-Aware Chunking

**Feature Branch**: `030-intent-aware-chunking`

**Created**: 2026-10-07

**Status**: Draft

**Input**: User description: "Chunk size should align with the query intent: Use large chunks for synthesis, comparisons, or summarization tasks to preserve broad context. Conversely, use small chunks for granular, fact-based queries targeting specific metrics, names, dates, or isolated configuration settings."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Precise answers to fact-based questions (Priority: P1)

An employee asks a granular question — for example, "What is the warranty period for the Pro plan?" or "When was the security policy last updated?" The system treats it as a targeted fact lookup and retrieves tightly-scoped content, so the answer pinpoints the exact fact with a precise citation instead of a broad excerpt.

**Why this priority**: Granular fact retrieval is the most common and highest-stakes query type. An imprecise or wrong fact erodes trust and is the primary source of hallucination risk, so getting this right first delivers the most value.

**Independent Test**: Can be fully tested by asking a set of fact-based questions whose answers live in isolated locations across several documents, and confirming each answer returns the exact fact with a single, correct citation.

**Acceptance Scenarios**:

1. **Given** a library where a specific metric (e.g., "renewal term: 30 days") is embedded in the middle of a large paragraph, **When** a user asks "What is the renewal term?", **Then** the system returns the exact figure with a citation to the precise location, not a broad excerpt.
2. **Given** a library containing multiple dates across documents, **When** a user asks "When did policy X take effect?", **Then** the answer states the exact date and cites the specific chunk containing it.
3. **Given** a granular question whose answer is not present anywhere in the library, **When** the user asks it, **Then** the system returns "no relevant content found" rather than a guess.

---

### User Story 2 - Comprehensive answers to synthesis questions (Priority: P2)

An employee asks a broad question — for example, "Summarize the onboarding process," "Compare the Starter and Enterprise plans," or "Give me an overview of our leave policy." The system treats it as a synthesis task and retrieves content with enough surrounding context to produce a complete, coherent answer that does not miss related facts that sit across adjacent passages.

**Why this priority**: Broad questions are where a fixed small chunk size most often fails — key relationships span passage boundaries — so widening context for these queries delivers a clear quality gain. It is lower-stakes than fact precision, hence P2.

**Independent Test**: Can be fully tested by asking comparison and summarization questions whose answers span multiple passages, and confirming each answer covers all relevant points without omission caused by passage boundaries.

**Acceptance Scenarios**:

1. **Given** a multi-section onboarding document, **When** a user asks "Summarize the onboarding process," **Then** the answer covers the major steps across sections, and no step is omitted because it straddled two passages.
2. **Given** two plans described in separate sections, **When** a user asks "Compare the Starter and Enterprise plans," **Then** the answer reflects both plans' features with citations to each.
3. **Given** a synthesis question whose relevant content spans several adjacent passages, **When** the user asks it, **Then** the system retrieves those passages together rather than fragmenting the answer.

---

### User Story 3 - Overriding the detected intent (Priority: P2)

When the system's automatic classification produces an answer that is too narrow or too broad, the employee can explicitly choose "Broad" or "Specific" and re-ask, without rephrasing the question. The default is "Auto" (the system decides), so most users never touch the control.

**Why this priority**: It provides a cheap, direct recovery from misclassification, letting the automatic behavior ship with a much lower risk of unrecoverable wrong-mode answers.

**Independent Test**: Can be fully tested by forcing "Broad" on a granular question and "Specific" on a synthesis question, and confirming the retrieval granularity follows the user's explicit choice.

**Acceptance Scenarios**:

1. **Given** an answer that is too narrow under Auto, **When** the user selects "Broad" and re-asks, **Then** the answer reflects broader context.
2. **Given** an answer that is too broad under Auto, **When** the user selects "Specific" and re-asks, **Then** the answer pinpoints the exact fact.
3. **Given** no override, **When** the user asks a question, **Then** the system classifies the intent automatically (default Auto).

---

### User Story 4 - Consistent grounding across all intents (Priority: P3)

Regardless of the detected intent or any user override, every generated answer remains grounded in cited sources, and answers with no relevant content are still handled by the existing "no relevant content found" rule. When intent cannot be confidently classified, the system degrades gracefully to a safe default rather than producing ungrounded text.

**Why this priority**: Citation grounding is a non-negotiable invariant. This story ensures the new adaptive behavior cannot weaken it.

**Independent Test**: Can be fully tested by verifying that answers across both intent types and all override states always carry citations (or the exact no-content message), and that ambiguous queries still return grounded answers.

**Acceptance Scenarios**:

1. **Given** a query whose intent is ambiguous, **When** the user asks it, **Then** the system still returns a grounded answer with citations, or "no relevant content found" — never ungrounded text.
2. **Given** a broad-context query, **When** the system answers, **Then** every claim carries a citation to its source (the larger unit).
3. **Given** a granular query, **When** the system answers, **Then** every claim carries a citation to its source (the smaller unit).

---

### Edge Cases

- What happens when a query mixes granular and broad elements (e.g., "Summarize the policy and tell me the effective date")?
- What happens when intent detection cannot confidently classify a query?
- What happens when the same fact appears in both a small and a large unit of content (duplicate or overlapping citations)?
- What happens when a broad query's relevant content spans multiple documents rather than passages within a single document?
- What happens when the library is empty or the query matches nothing at either granularity?
- What happens when an existing library is re-ingested into the two-level structure without duplicating or losing content?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST classify each incoming query as broad-context (synthesis, comparison, summarization) or granular (specific metric, name, date, or isolated configuration lookup).
- **FR-002**: For queries classified as broad-context, System MUST retrieve content in larger units that preserve surrounding context sufficient for synthesis and comparison.
- **FR-003**: For queries classified as granular, System MUST retrieve content in smaller, tightly-scoped units so the precise fact is isolated.
- **FR-004**: System MUST keep every answer citation-grounded, regardless of intent, citing the larger unit for broad answers and the smaller unit for granular answers.
- **FR-005**: System MUST return exactly "no relevant content found" when no relevant content exists, and MUST NOT fabricate an answer.
- **FR-006**: Intent classification and granularity selection MUST complete without any query-time external network access (offline invariant).
- **FR-007**: Users MUST be able to override the automatically-detected intent with an explicit "Broad" or "Specific" choice; the default MUST be "Auto" (the system decides).
- **FR-008**: When intent cannot be confidently determined, System MUST fall back to a safe default granularity and still return a grounded answer.
- **FR-009**: System MUST apply intent-aware granularity within the existing retrieval flow (topK limit and relevance floor still apply), so result relevance is preserved.
- **FR-010**: Ingest MUST store each document's content at two granularities: small units (~512 tokens) and larger units (~2048 tokens) that each group several small units together.
- **FR-011**: Re-ingesting existing library content into the two-level structure MUST preserve all content with no data loss or duplication, and MUST retain the existing background-ingest lifecycle.

### Key Entities *(include if feature involves data)*

- **Document**: The source content; unchanged in meaning, re-unitized at two granularities.
- **Child Chunk**: The small, precise unit (~512 tokens) used for granular fact retrieval.
- **Parent Chunk**: The larger contextual unit (~2048 tokens) that groups several child chunks, used for synthesis retrieval.
- **Query**: A user-submitted question, associated with a detected intent and an optional user override.
- **Query Intent / Mode**: The classification (broad-context vs granular) plus the Auto/override state that governs which granularity of content is retrieved.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: At least 90% of granular queries return the exact target fact with a correct citation on a curated fact-based evaluation set.
- **SC-002**: At least 90% of broad queries produce answers that cover all relevant points without omission across passage boundaries on a curated synthesis evaluation set.
- **SC-003**: Retrieval precision on the existing curated 50-question evaluation set remains at or above 80% top-5 containing the relevant document.
- **SC-004**: 100% of generated answers remain citation-grounded or return exactly "no relevant content found" (no ungrounded answers).
- **SC-005**: Granularity selection adds no perceivable delay — queries continue to return results within the existing latency envelope.
- **SC-006**: Re-ingestion of the existing library completes without losing or duplicating any document's retrievable content.

## Assumptions

- The classification taxonomy is the two categories described (broad-context vs granular), with the listed examples being illustrative rather than exhaustive.
- Small units remain the ~512-token chunks used today; large units are fixed ~2048-token groupings of roughly four small units (no document-section structure detection in v1).
- Large units are derived by grouping small units at ingest, rather than stored as a second, independently searchable index.
- Existing library content is re-processed into the two-level structure; re-ingestion is transparent to users (no re-entry) and preserves the background-ingest lifecycle.
- The override control is surfaced in the desktop client's Ask surface as a Broad/Auto/Specific selection defaulting to Auto.
- Citation format, the topK limit, and the relevance-floor behavior are otherwise unchanged.
