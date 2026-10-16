# Feature Specification: Reasoning Progress While Waiting

**Feature Branch**: `032-reasoning-progress`

**Created**: 2026-10-15

**Status**: Implemented and verified (2026-10-16)

**Input**: User description: "End user should see the reasoning while waiting for an answer"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The wait is explained, not blank (Priority: P1)

An employee asks a question and, instead of watching a single unchanging "working…" indicator for several seconds, sees a short, live account of what the workstation is doing — that it is reading the question, searching the library, finding what it found, and composing the answer. Each line appears as that step actually happens, so the wait feels like progress rather than a freeze.

**Why this priority**: Today the only feedback is one static line and a spinner. Local models on modest workstation hardware can take many seconds per answer, and an unexplained pause is the moment users assume the system has hung, give up, or re-submit the same question. Making the wait self-explanatory is the whole point of the feature and delivers value on its own, even if nothing else here ships.

**Independent Test**: Can be fully tested by asking a question against a real library and confirming that the user sees more than one distinct progress update appearing over time before the answer lands, and that the last row of progress corresponds to the work actually performed.

**Acceptance Scenarios**:

1. **Given** a signed-in employee with a query in flight, **When** the question is submitted, **Then** the conversation area immediately shows an account of the work in progress that updates at least once before the answer arrives.
2. **Given** an answer that takes several seconds, **When** the employee watches the screen, **Then** the displayed reasoning changes as the work advances (not a single frozen message from submit to answer).
3. **Given** the answer has arrived, **When** the employee looks at the conversation, **Then** the reasoning account is gone and the answer with its citations is shown exactly as it is today.
4. **Given** the employee navigates away from the page while an answer is in flight, **When** they return, **Then** the in-flight reasoning does not reappear as a stale artifact.
5. **Given** an answer that takes several seconds, **When** the employee watches the wait, **Then** they see, in order, that the question is being understood, that the library is being searched, which/how many relevant sources were found, and that the answer is being composed — each appearing as that work actually happens.

---

### User Story 2 - The reasoning is honest and safe (Priority: P2)

Whatever the employee is shown must be a truthful description of work actually performed, phrased for a non-technical reader, in the same language as the question, and must never surface raw internals or content the employee is not entitled to see. It must never claim a step finished before it did, and never describe work that did not happen.

**Why this priority**: An explanation that lies is worse than no explanation: it destroys the trust the feature exists to build. It is second only to the feature existing, because it constrains *what* is shown rather than whether anything is shown.

**Independent Test**: Can be fully tested by scripting slow, failing, and no-result answers and checking that every displayed line corresponds to a stage that genuinely ran, uses no internal jargon, matches the question's language, and contains no content from records about other people.

**Acceptance Scenarios**:

1. **Given** a question asked in Greek, **When** the progress is displayed, **Then** every progress line is in Greek; and for an English question, every line is in English.
2. **Given** an answer that ends in exactly "no relevant content found", **When** the employee watched the wait, **Then** the progress never claimed that sources were found for that question.
3. **Given** a library holding same-template records about several different people, **When** the employee asks about one named person, **Then** no progress line reveals content or names belonging to another person's record.
4. **Given** any wait, **When** the employee reads the progress, **Then** no line exposes raw prompt text, internal identifiers, model settings, or document bodies beyond what the answer's own Sources view already shows.
5. **Given** the request fails partway (workstation unreachable, model unavailable, timeout), **When** the failure is reported, **Then** the employee sees the same failure message as today and no misleading "still working" reasoning remains on screen.

---

### User Story 3 - Reasoning stays consistent with the finished answer (Priority: P3)

Once the answer is delivered, the conversation must look and behave exactly as it does today: the answer, its citations, its "Sources" expander, its place in history, and its copy action are unchanged. The reasoning is a transient companion to the wait, not a second body of content that competes with the answer or changes what is stored.

**Why this priority**: Citation grounding and history integrity are non-negotiable invariants. This story guarantees the new behaviour cannot weaken either. It is last because it is a guardrail on the other two, not a new capability.

**Independent Test**: Can be fully tested by comparing a conversation recorded today with one recorded after the change: the message order, answer text, citation list, and history detail for each query are identical.

**Acceptance Scenarios**:

1. **Given** an answered question, **When** the employee opens it later from history, **Then** they see the same answer and citations as today, and the answer remains citation-grounded or exactly "no relevant content found".
2. **Given** a question that matched nothing in the library, **When** the employee asks it, **Then** the result is still exactly "no relevant content found" with no citations.
3. **Given** a completed exchange, **When** the employee copies the answer, **Then** only the answer text is copied as today, never progress lines.
4. **Given** an exchange that included a visible reasoning account, **When** the employee revisits it later from the conversation or from history, **Then** no part of that reasoning account is shown or retrievable — only the answer and its citations remain.

---

### Edge Cases

- What happens when the answer arrives faster than the progress can render (very small library, cached model)? The user must not be shown a flash of misleading or contradictory steps, and must never be left with a stale progress line after the answer lands.
- What happens when the answer takes far longer than usual (cold model load, first question after startup)? The progress must keep the wait believable without inventing steps or implying a stall has occurred.
- What happens when the employee submits a second question while the first is still in flight? The behaviour must match today's busy-state rules and must not interleave two reasoning accounts.
- What happens when the workstation is unreachable from the very first moment? The failure must be reported as today, with no progress that suggests work began.
- What happens when the library is empty or the question matches nothing? The progress must not narrate retrieval that found nothing as if it had found sources.
- What happens when the whole page is closed and reopened while a question is in flight? No orphaned progress may persist across the page lifetime.
- What happens on a workstation with several questions answered concurrently across different employees? Each employee's progress must describe only their own request and never another user's activity.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: While a question is in flight, the desktop client MUST display an account of the work being performed instead of only an indeterminate busy indicator.
- **FR-002**: The displayed reasoning MUST consist of named work stages in plain language together with the sources consulted — at minimum: that the question is being understood, that the library is being searched, how many relevant sources were found and which documents they are (distinct documents, named by filename), and that the answer is being composed. It MUST describe this work only; it MUST NOT reproduce the answering model's internal thinking text.
- **FR-003**: The displayed reasoning MUST update progressively as the work advances, so the employee sees change over time rather than one static message held from submission to answer.
- **FR-004**: Each reasoning update MUST be reported live from the workstation at the moment the corresponding work actually happens, rather than being pre-scripted by the client or advanced on a fixed timer.
- **FR-005**: The reasoning MUST describe only work that actually occurred, at the moment it occurred; it MUST NOT announce a stage as complete before it is, or describe steps that never ran.
- **FR-006**: The reasoning MUST be written in plain language for a non-technical employee — no internal jargon, identifiers, or infrastructure names — and MUST use the same language as the question.
- **FR-007**: The reasoning MUST NOT expose raw prompt text, internal chunk/document bodies beyond what the answer's own Sources view already shows, model configuration, or any content belonging to another person or record.
- **FR-008**: The reasoning MUST be scoped to the requesting employee only, exactly like the answer; it MUST never contain or reveal another employee's question, activity, or library access.
- **FR-009**: The reasoning MUST disappear once the answer or a failure is presented, leaving the conversation in exactly the state it has today (answer text, citations, Sources, copy action). (Its non-persistence is FR-014.)
- **FR-010**: The reasoning MUST NOT alter the stored answer, its citations, its position in conversation or history, or the "no relevant content found" rule.
- **FR-011**: The reasoning MUST be produced without any query-time access outside the company's own network, preserving the offline invariant.
- **FR-012**: When the request fails (workstation unreachable, model unavailable offline, timeout), the employee MUST see the same failure reporting as today, with no lingering progress display.
- **FR-013**: When a question is answered too quickly for meaningful progress to be shown, the system MUST NOT display misleading, contradictory, or flickering progress lines.
- **FR-014**: The reasoning MUST NOT be retained beyond the wait: it is discarded as soon as the answer or a failure is presented, is never stored with the query, and is never shown again from the conversation or history. (Its on-screen removal is FR-009.)

### Key Entities *(include if feature involves data)*

- **Reasoning Update**: One transient statement of work in progress shown to the employee while a question is in flight — its plain-language text, the stage it describes, its position in the sequence, and when it was shown.
- **Reasoning Account**: The ordered sequence of Reasoning Updates for a single in-flight question; it exists only for the duration of the wait and is discarded when the answer or failure is presented. It is not part of the stored Query.
- **Query**: The employee's question, its answer, and its citations — unchanged by this feature.
- **Answer**: The grounded, cited result already produced today; its content, citations, and history behaviour are unchanged.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For every question whose answer takes longer than two seconds, the employee sees at least two distinct reasoning updates before the answer arrives (100% of sampled slow answers).
- **SC-002**: The first reasoning update appears within one second of submitting a question, for at least 95% of questions on a workstation meeting the deployment hardware baseline (16 GB RAM, GPU where available).
- **SC-003**: In a scripted session of 20 deliberately slow answers, employees re-submit the same question while waiting zero times (down from today's behaviour).
- **SC-004**: In a usability check with at least five employees, at least four state they could tell what the system was doing during the wait and did not believe it had stalled.
- **SC-005**: 100% of answers remain citation-grounded or exactly "no relevant content found" on the existing curated evaluation set — grounding is unchanged by this feature.
- **SC-006**: A review of 20 sampled queries that reference named people finds zero progress lines exposing another person's content.
- **SC-007**: The employee-visible time from submitting a question to the answer being complete increases by no more than 5% versus today, measured on the same question set against the same library with a warm model, comparing the streamed path with the existing one. Because per-answer time is dominated by model generation, the feature's own cost is read as response time minus the pipeline's own recorded latency, which isolates the transport from the model.
- **SC-008**: The offline verification suite continues to pass unchanged, confirming no query-time access outside the company network.

## Assumptions

- "Reasoning" here means a plain-language account of the work in progress together with the sources consulted (a live progress narrative), not the answering model's internal thinking text and not the answer text appearing progressively.
- Reasoning updates are reported live from the workstation as each stage happens; the client does not pre-script or timer-advance them.
- The reasoning account is ephemeral: it is discarded the moment the answer or a failure is presented and is never stored with the query or shown from history.
- The employee-facing surface is the existing desktop client's question-and-answer conversation; no new screen is introduced.
- Progress is displayed in the conversation area where the busy indicator sits today: the in-stream busy text line is replaced rather than supplemented. The send button's own in-flight spinner is a separate, pre-existing indicator and stays.
- The workstation remains the only party performing AI work; the desktop stays a thin client and no AI processing moves to the desktop.
- No stop/cancel control is added in this feature; the employee can still navigate away, which behaves as today.
- Answers continue to be stored and retrievable exactly as they are today; only the ephemeral wait experience changes.
- Answers are generated in the same language as the question (existing behaviour) and progress lines follow it.
- This feature applies to the desktop client only; no other client surface is in scope.
- SC-003 and SC-004 are post-launch outcome measurements (a scripted session and a usability check), not buildable acceptance criteria; they are tracked manually after release.
- SC-002 and SC-006 are timing and sampling observations rather than automated assertions: SC-002 is recorded during the manual stream check in `quickstart.md` §2, and SC-006 is a review of sampled queries. SC-001, SC-005, SC-007 and SC-008 are covered by automated or scripted checks.
