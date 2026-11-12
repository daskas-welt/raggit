# Feature Specification: Document Output from the Ask View

**Feature Branch**: `033-ask-document-generation`

**Created**: 2026-11-01

**Status**: Draft

**Input**: User description: "At Ask view, end-users should be able to make queries on the uploaded docs but also can ask to create word documents, excel documents, pdfs and output text"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ask for a document, receive one (Priority: P1)

An employee is working in the Ask view and needs a deliverable, not a chat reply. They type an ordinary request — "create a Word document with the employment history of Μαρία Παπαδοπούλου" — and the conversation answers as it always does, but it also hands them a finished file in the requested format. It is named after the subject, carries the same grounded content and the same sources as the answer, and they can save it and open it in their usual word processor.

**Why this priority**: This is the request itself. Without a produced file, every other story here is decoration. It delivers the whole value — turning a grounded answer into something an employee can attach to an email or print — even if nothing else ships.

**Independent Test**: Can be fully tested by asking for a Word document about a subject the library covers, saving the result, opening it in a standard word processor, and comparing its content and sources with the answer shown in the conversation.

**Acceptance Scenarios**:

1. **Given** a signed-in employee and a library containing records about a subject, **When** they ask for a Word document about that subject, **Then** the conversation shows the answer and offers a Word file named after the subject that can be saved and whose content matches the answer shown.
2. **Given** the same request for a PDF, **When** the employee saves and opens the file, **Then** it opens in a standard PDF reader and shows the same content and sources as the answer.
3. **Given** the same request for a plain-text output, **When** the employee saves the file, **Then** it opens in a text editor and contains the answer text and its sources as readable text.
4. **Given** a request that names no format ("write me a report about X"), **When** the employee submits it, **Then** the conversation answers as it does today and no file is produced, with saving available on that answer (User Story 3).
5. **Given** a request for a document about something the library does not cover, **When** it is submitted, **Then** the answer is exactly `no relevant content found` and no file is offered.
6. **Given** a request typed in Greek, **When** the document is produced, **Then** its title, headings and body are in Greek; an English request produces an English document.

---

### User Story 2 - Spreadsheets that behave like spreadsheets (Priority: P2)

When an employee asks for an Excel document listing something — people and their fields, totals, a comparison — they get a real table they can sort, filter and calculate with, not a paragraph pasted into the first cell. Each column has a meaningful header, each record has its own row, and a value the library does not record is left visibly empty rather than invented.

**Why this priority**: Spreadsheet output is only valuable if it is usable as data. A malformed sheet is worse than a refusal, and tables are the most likely reason an employee asks for Excel in the first place.

**Independent Test**: Can be fully tested by asking for a spreadsheet listing several known records with two or more fields, then opening it and confirming the header row, one row per record, and that fields absent from the library are empty rather than filled in.

**Acceptance Scenarios**:

1. **Given** a request for a spreadsheet that lists several records, **When** the file is produced, **Then** it has one header row naming each field and one row per record, with each value in its own cell.
2. **Given** a record for which the library holds no value for a requested field, **When** the sheet is produced, **Then** that cell is empty (or explicitly marked as not recorded) and never contains a guessed value.
3. **Given** a request about a single record, **When** it is produced as a spreadsheet, **Then** the file still has a header row and exactly one data row.
4. **Given** a request whose content is prose rather than records ("summarize the policy"), **When** the employee asks for a spreadsheet, **Then** the file is still a usable sheet with clearly titled sections rather than a broken grid.
5. **Given** a sheet holding dates and amounts, **When** it is opened in a spreadsheet application, **Then** those cells are recognized as dates and numbers and can be sorted or used in calculations without retyping.

---

### User Story 3 - Any finished answer can become a file (Priority: P3)

An employee reads an answer they did not ask to receive as a document and decides they need it as a file after all. They choose the format from the answer itself — Word, Excel, PDF or text — and get the same content as a saveable file. The same is available for answers they reopen from their history.

**Why this priority**: It removes the need to guess the right wording up front and makes the capability useful for the answers that already exist. It is separable: the capability is valuable without it, and it can ship after the first two stories.

**Independent Test**: Can be fully tested by taking an ordinary answer with citations, saving it in each of the four formats, and comparing each saved file with the on-screen answer.

**Acceptance Scenarios**:

1. **Given** any finished answer in the conversation, **When** the employee chooses to save it as Word, Excel, PDF or text, **Then** a file in that format is produced whose content matches the answer.
2. **Given** an answer opened from history, **When** the employee saves it, **Then** the file matches the answer and carries the same sources.
3. **Given** an answer whose citations exist, **When** it is saved in any format, **Then** the file lists the same sources as the answer.
4. **Given** the answer is exactly `no relevant content found`, **When** the employee looks at the answer, **Then** no save action is offered for it.
5. **Given** a save is cancelled in the file dialog, **When** the employee returns to the app, **Then** no file has been created and the conversation is unchanged.

---

### User Story 4 - Documents tell the truth and stay private (Priority: P4)

A generated document is a business document: it must contain only what the library supports, must never mix in another person's record, and must go only where the employee puts it. Nothing generated is silently added to the shared library, nothing is left behind on the workstation, and a failure never leaves a corrupt file behind.

**Why this priority**: The trust model is the product. A document that invents a fact, blends two people's records, or quietly publishes itself to the whole company would do more damage than not having the feature. It is last because it constrains the other three rather than adding a capability.

**Independent Test**: Can be fully tested by generating documents about people who share a record template, comparing every statement and cell against the cited sources, and confirming the library's contents are unchanged afterwards.

**Acceptance Scenarios**:

1. **Given** a generated document, **When** each factual statement is checked against the sources it lists, **Then** every statement is supported by one of them and none is invented.
2. **Given** a library holding same-template records about several different people, **When** a document is generated about one named person, **Then** it contains no content belonging to another person's record.
3. **Given** a document was generated, **When** another employee searches the shared library, **Then** the generated file is not present and cannot be found there.
4. **Given** a generation that fails partway, **When** the failure is reported, **Then** the employee sees a clear, user-safe message and no file is offered or left on disk.
5. **Given** the employee saves to a name that already exists, **When** the file dialog confirms, **Then** the existing file is only replaced with the employee's explicit confirmation.
6. **Given** the employee's workstation is unreachable or the model is unavailable, **When** they request a document, **Then** they see the same failure reporting as an ordinary question today, with no file.

---

### Edge Cases

- A single request asks for more than one thing ("compare X and Y and put it in Excel"): the document must cover the whole request, or say plainly what it could not cover — never silently drop half of it.
- The employee asks for a format this feature does not offer (for example PowerPoint): the system must say which four formats are supported and still answer in the conversation, rather than substituting a different format without saying so.
- The library holds the answer but not in the shape requested (asking for a table where only prose exists, or for a field that is never recorded): the employee must get a usable document or a clear explanation, never a fabricated column or an unusable file.
- The result is far larger than expected (hundreds of records): the file must still open and be usable, or the employee must be told the request is too large and asked to narrow it.
- A document is saved while the same file is open in another application: the employee must see a clear message and be able to retry, not a crash or a silently truncated file.
- The subject's name contains characters that are invalid in a filename: the suggested filename must be valid for the operating system.
- The library is empty, or the question matches nothing: same refusal as today, and no file.
- A second document request is submitted while one is still being generated: the existing busy behavior applies; two documents must never be mixed into one file.
- The employee leaves the page while a document is being generated: nothing orphaned or half-written is left behind, and returning to the page shows no stale file.
- The employee is signed in but has no access to the subject: the document must obey the same per-person access rules as the answer.

## Requirements *(mandatory)*

### Functional Requirements

**Producing documents**

- **FR-001**: From the Ask view, an employee MUST be able to request a document in each of four formats: Word document, Excel spreadsheet, PDF, and plain text.
- **FR-002**: The requested format MUST be understood from the request as typed in ordinary language (for example "Word", "docx", "spreadsheet", "Excel", "PDF", "text"), without requiring the employee to operate a separate mode or settings screen before asking.
- **FR-003**: The content of a generated document MUST be the answer the conversation shows for that same request — the same facts, figures, names and citations, and the same sources — arranged into the requested document shape; formatting and layout MUST NOT change facts, figures, names or citations. The document MUST NOT be a second, independent generation of the request whose wording diverges from the displayed answer.
- **FR-004**: A generated document MUST carry a title and the list of sources it used, in a form a reader can check against the library.
- **FR-005**: The document MUST be written in the same language as the question, matching the existing answer behavior.
- **FR-006**: The suggested filename MUST be derived from the document's subject, MUST be valid on the employee's operating system, and MUST be changeable by the employee when saving.
- **FR-007**: When a request names no format, the system MUST answer in the conversation exactly as it does today and MUST NOT produce a file unasked.

**Spreadsheet content**

- **FR-008**: A spreadsheet MUST be organized as a table: one header row naming the fields, one row per record, and one value per cell.
- **FR-009**: A field the library does not record for a record MUST be left empty or explicitly marked as not recorded; the system MUST NOT guess, infer or carry a value over from another record.
- **FR-010**: Dates, amounts and quantities in a spreadsheet MUST be stored so a spreadsheet application recognizes them as dates and numbers rather than as plain text.
- **FR-011**: When the requested content is not naturally tabular, the spreadsheet MUST remain usable — for example as a sheet with clearly titled sections — rather than forcing prose into a single cell or an arbitrary grid.

**Saving and delivery**

- **FR-012**: The employee MUST be able to save a generated document to a location of their choosing on their own machine, and to open it from the conversation.
- **FR-013**: Saving MUST go through the operating system's standard file dialog; the system MUST NOT write files to locations the employee did not choose, overwrite an existing file without explicit confirmation, or leave a partial file behind when a save is cancelled or fails.
- **FR-014**: A generated document MUST NOT be added to the shared library, MUST NOT be retained by the system beyond delivering the file to the employee, and MUST NOT be visible to any other employee; it exists only where the employee saved it. Transient processing while generating and delivering the file is permitted; nothing about it is kept once the employee saves it or abandons the save.
- **FR-015**: The conversation and history MUST retain the question, answer and citations exactly as today; generated files themselves are not retained and can be produced again by re-asking.

**Any answer as a file**

- **FR-016**: Any finished answer in the conversation MUST be savable in any of the four formats, and the same MUST be possible for an answer reopened from history.
- **FR-017**: A saved ordinary answer MUST match the on-screen answer in content and sources; the only permitted differences are formatting and layout.
- **FR-018**: The refusal answer `no relevant content found` MUST NOT offer a save action, and a request for a document that matches nothing MUST produce no file.

**Trust, privacy and operations**

- **FR-019**: Every factual statement and every cell in a generated document MUST be supported by the sources the document lists; the system MUST NOT invent facts, values, names, dates or citations.
- **FR-020**: A document generated about one person MUST NOT contain content from another person's record, including when those people share a record template.
- **FR-021**: Document generation MUST happen with no access outside the company's own network, preserving the offline invariant, and the desktop MUST remain a thin client with no AI work moved onto it.
- **FR-022**: When generation fails, the employee MUST see a clear, user-safe message that does not expose internals, and MUST NOT be offered a partial, empty or corrupt file.
- **FR-023**: Document generation MUST follow the existing busy and error behavior of the Ask view: one request at a time, the same failure vocabulary, and no state that survives leaving the page.
- **FR-024**: A request for a format outside the four supported ones MUST be explained as unsupported, and the answer MUST still be delivered in the conversation rather than silently converted to another format.
- **FR-025**: A request whose result would be too large to produce a usable document MUST be reported to the employee with a request to narrow it, rather than producing an unusable file.

### Key Entities *(include if feature involves data)*

- **Document Request**: The employee's ask, the format they want, and the subject the document is about. It belongs to one employee and one conversation turn.
- **Generated Document**: The produced artifact — its format, title, body or table, the sources it used, and the moment it was produced. It is handed to the employee and then exists only where the employee saved it; it is not part of the stored Query.
- **Document Table**: The spreadsheet-specific shape of a generated document — header fields and one set of values per record, with absent values left empty rather than filled.
- **Answer**: The grounded result already produced today; its text, citations, history behavior and refusal rule are unchanged by this feature.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of document requests whose subject exists in the library produce a file in the requested format that opens without a repair prompt in the corresponding standard application (sample: 20 requests spread across the four formats).
- **SC-002**: Zero files are produced for questions the library cannot support; those answers remain exactly `no relevant content found`.
- **SC-003**: In a sample of 30 generated documents checked against the sources they list, zero statements or cell values are unsupported by those sources.
- **SC-004**: In a sample of 20 documents about named people who share a record template, zero contain content belonging to another person's record.
- **SC-005**: Saving any finished answer in any of the four formats yields content matching the on-screen answer in 100% of a 20-answer sample.
- **SC-006**: Producing a document takes no more than 15% longer than answering the same question as text alone, measured over 10 questions on the same workstation with the model already loaded.
- **SC-007**: In a usability check with at least five employees, at least four produce and save a document on their first attempt without assistance.
- **SC-008**: The shared library contains exactly the same documents before and after a session in which documents were generated.
- **SC-009**: The offline verification suite passes unchanged, confirming no access outside the company network at document-generation time.
- **SC-010**: In a scripted session, a cancelled save, a failed generation and an unsupported-format request each leave zero files behind and show one clear message.

## Assumptions

- **Format is chosen by the wording of the request.** There is no separate document-generation mode or settings screen: the employee says what they want, as they would to a colleague. If no format is named, behavior is exactly today's.
- **"Output text" means both of today's behaviors and one new one**: the plain answer in the conversation (unchanged) and the ability to save that text as a `.txt` file.
- **Documents are grounded in the uploaded library, with the same citation and refusal rules as answers.** Headings, layout and connecting prose may be composed, but facts, names, figures and dates come from the library and are cited. This is what keeps a generated document a defensible business document rather than a plausible-looking guess.
- **Generated files belong to the employee who asked.** They are saved to the employee's own machine and are never added to the shared library, retained by the system beyond delivery, or listed for other employees. Re-asking regenerates them.
- **Saving uses the operating system's file dialog** and the employee's own disk, consistent with how uploading and downloading library documents already work.
- **Spreadsheet shape follows the content**: tabular when the request is about records, titled sections when the content is prose. One workbook per request; custom templates, branding and multi-sheet structures are out of scope.
- **"Any finished answer" covers the current conversation and answers reopened from history.** It is delivered as one capability, but may ship after document requests themselves.
- **Only the four formats are in scope.** Other formats (presentations, images, email drafts) are out of scope; the employee is told they are unsupported.
- **One document per request.** Requests asking for several separate files are answered as one document covering the request, or the employee is asked to split them.
- **No in-app editing, scheduling, batch generation, mail merge or signature workflows** in this feature; the employee edits the saved file in their own tools.
- **The workstation remains the only party doing AI work**; the desktop produces no content and holds no model. Document assembly itself must not require new external services or network access.
- **Existing surfaces and rules are unchanged**: the Ask conversation, citations, history, per-person access and the `no relevant content found` rule keep their current behavior; this feature adds an output path beside them.
- **SC-006, SC-007 and SC-010 are measured through the manual and scripted checks recorded in the feature's quickstart/verification material**, not asserted in unit tests; SC-001 to SC-005, SC-008 and SC-009 are covered by automated or scripted checks.
- **The document presents the answer the conversation displays, not a second generation.** A document request produces one grounded answer, shown in the conversation and arranged into the requested file (FR-003). This is what makes the US-1 acceptance comparison meaningful and SC-006's overhead bound achievable; `/speckit.clarify` may revise it, but any revision must weaken FR-003's "same content" wording accordingly.
