# Feature Specification: Description-Based Document Creation

**Feature Branch**: `034-prompted-document-creation`

**Created**: 2026-11-17

**Status**: Draft

**Input**: User description: "Users should be able to create new documents by providing a description what they want to achieve. Documents can be txt, pdf, docx, xlsx. New documents may be based on existing documents uploaded to raggit and should be relevant to them"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Create a document from a description (Priority: P1)

A signed-in employee describes the document they need, chooses TXT, PDF, DOCX, or XLSX, and receives a new file they can save on their own device. This is a new document-creation task, separate from saving an existing Ask answer.

**Why this priority**: It delivers the central request: turn a user's goal into a usable document without requiring them to first write an Ask question or have an existing answer.

**Independent Test**: Describe a short, self-contained deliverable, choose each supported format in turn, save the result, and confirm each file opens in an ordinary application for that format and addresses the requested goal.

**Acceptance Scenarios**:

1. **Given** a signed-in employee with a valid description, **When** they choose DOCX and create the document, **Then** they receive a saveable Word document that addresses the description.
2. **Given** the same request in PDF, TXT, or XLSX, **When** the employee creates it, **Then** the received file uses the selected format and contains the requested content.
3. **Given** a description in a language supported by the workstation, **When** a document is created, **Then** its title and body use that language unless the description requests another.
4. **Given** the employee has not entered a description or selected a format, **When** they try to create a document, **Then** creation does not start and the missing information is identified.
5. **Given** an employee already has an Ask answer, **When** they start a new document-creation request, **Then** the new document is based on the new description and is not merely an export of that prior answer.

---

### User Story 2 - Create a document that uses relevant library material (Priority: P2)

An employee describes a deliverable that relates to material already uploaded to RAGGit. The document uses relevant library content, while making its sources visible so the employee can check the result.

**Why this priority**: Using the library makes new deliverables useful in the employee's real work and distinguishes them from generic documents composed only from the description.

**Independent Test**: Upload a known source document, request a deliverable that names its topic, and check that the generated document uses relevant facts from that source and identifies it. Repeat with unrelated documents and records about different people to check that they do not contaminate the result.

**Acceptance Scenarios**:

1. **Given** relevant uploaded material and a description that asks to use it, **When** a document is created, **Then** relevant facts are supported by that material and the document identifies the sources used.
2. **Given** uploaded material that is unrelated to the description, **When** a document is created, **Then** unrelated material is not introduced into the document.
3. **Given** no relevant uploaded material, **When** an employee asks for a document, **Then** the system makes clear that no library sources were used and does not imply that the document is library-grounded.
4. **Given** two people have records with the same structure, **When** a document is requested about one person, **Then** the document contains no facts from the other person's record.
5. **Given** relevant source documents disagree on a fact needed by the requested deliverable, **When** the document is created, **Then** the disagreement is made clear rather than silently resolved by inventing or blending a value.

---

### User Story 3 - Save a complete and usable result safely (Priority: P2)

The employee saves the generated result to a location they choose, then opens and uses it in the corresponding application. Cancelling or encountering a failure does not leave an unwanted or damaged file.

**Why this priority**: A generated document only helps if it can be delivered in a usable format without silently writing to a location the employee did not choose.

**Independent Test**: Generate one document of each format, save and open each, then repeat with a cancelled save and a forced generation or write failure; confirm no partial file is left behind.

**Acceptance Scenarios**:

1. **Given** a completed document, **When** the employee saves it, **Then** the employee chooses its location and may change its suggested filename.
2. **Given** a file already exists at the chosen destination, **When** the employee saves using that name, **Then** the existing file is replaced only after the employee confirms.
3. **Given** the employee cancels the save or generation fails, **When** control returns to the employee, **Then** no partial or unwanted file is left behind and a clear, user-safe explanation is shown.
4. **Given** the document is generated, **When** the employee or another employee searches the RAGGit library or opens document history, **Then** the generated file is not present there.

## Edge Cases

- The description is empty, only whitespace, or too large to process: do not start generation; identify the problem in plain language.
- The employee requests an unsupported format such as a presentation: explain that only TXT, PDF, DOCX, and XLSX are available; do not silently substitute a format.
- The description names a particular library document that cannot be found: do not claim to have used it; clearly say that no matching source was used.
- The library has no relevant material: allow description-only creation, but clearly distinguish it from a source-backed document.
- Library sources contain conflicting values, missing fields, or ambiguous dates or amounts: show the gap or conflict rather than guessing.
- A request about one person could match records about other people with the same template: include only the requested person's supported information.
- The requested spreadsheet is prose rather than records: keep it readable, using titled sections instead of forcing prose into arbitrary columns.
- The description requests more content than can be produced as a usable file: explain the limit and ask the employee to narrow the request.
- The employee cancels, loses workstation connectivity, or the local model is unavailable: do not offer a partial file or leave one behind.
- A suggested filename contains characters invalid on the employee's device: provide a valid suggested filename that can be changed before saving.
- Text inside an uploaded source document attempts to redirect the task or request unrelated library content: treat the document as reference material and do not let it override the employee's request or access rules.

## Requirements *(mandatory)*

### Functional Requirements

**Creating and formatting**

- **FR-001**: Signed-in employees and administrators MUST be able to start a new document-creation request independently of exporting an existing Ask answer.
- **FR-002**: The employee MUST be able to describe the goal and intended content of the document in their own words.
- **FR-003**: The employee MUST be able to choose exactly one output format for each request: plain text (`.txt`), PDF (`.pdf`), Word (`.docx`), or Excel (`.xlsx`).
- **FR-004**: The system MUST create one new document from the description and selected format; it MUST NOT require an existing Ask answer.
- **FR-005**: A generated document MUST have a meaningful title, preserve the requested purpose and material constraints, and use the requested language when specified.
- **FR-006**: An XLSX document requested for tabular content MUST use meaningful column headings and separate values into cells; when the request is not naturally tabular, it MUST use readable titled sections.
- **FR-007**: The system MUST prevent creation when the description or output format is missing and explain what the employee needs to provide.
- **FR-008**: The system MUST explain that only TXT, PDF, DOCX, and XLSX are supported when an unsupported format is requested, and MUST NOT silently produce a different format.

**Use of uploaded documents**

- **FR-009**: The system MUST be able to use relevant documents already uploaded to the RAGGit library when the employee's description requests or clearly depends on library material.
- **FR-010**: When library material is used, the generated document MUST identify the source documents and every factual claim attributed to those documents MUST be supported by relevant source content.
- **FR-011**: The system MUST NOT use unrelated library material to fill gaps in the request, and MUST NOT transfer facts between different people's records, including records with the same structure.
- **FR-012**: When no relevant library material is found, the system MUST make clear that no library sources were used and MUST NOT present description-only content as verified by the library.
- **FR-013**: When relevant sources disagree or omit a requested value, the system MUST disclose the conflict or missing information rather than inventing, guessing, or borrowing a value from another record.
- **FR-014**: Uploaded document content MUST be treated as reference material and MUST NOT override the employee's request or the system's access and privacy rules.

**Delivery, privacy, and operation**

- **FR-015**: The employee MUST be able to save the generated file to a location of their choosing, change its suggested filename, and use the operating system's standard save confirmation before replacing an existing file.
- **FR-016**: Cancelling a save or encountering a generation or save failure MUST NOT leave a partial file behind; the employee MUST receive a clear message that does not expose internal details.
- **FR-017**: Generated files MUST NOT be added to the shared library or retained as generated documents in RAGGit history; the employee's chosen local save is the only retained copy.
- **FR-018**: Document generation and use of the uploaded library MUST remain within the company's own network and MUST NOT require WAN access.
- **FR-019**: A suggested filename MUST be valid on the employee's device, describe the requested deliverable, and remain editable by the employee.
- **FR-020**: The new creation flow MUST leave the existing Ask answer-export behavior and its saved answers unchanged.

### Key Entities *(include if feature involves data)*

- **Document Creation Request**: The employee's description and selected output format for one new deliverable. It is separate from an existing Ask answer.
- **Generated Document**: One formatted result created for a request and delivered for the employee to save. It is not added to the RAGGit library or generated-document history.
- **Library Source**: An uploaded document whose relevant content supports a generated document; its identity is shown when used.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In a sample of 20 valid requests (five per supported format), 20 of 20 resulting files open in a standard application for their format without a repair prompt.
- **SC-002**: In a sample of 30 source-backed documents reviewed against their listed library sources, zero factual claims attributed to those sources are unsupported.
- **SC-003**: In tests involving at least 20 requests about people with similarly structured records, zero generated documents contain facts from the wrong person's record.
- **SC-004**: In a walkthrough with at least five employees, at least four can create and save a document on their first attempt without assistance.
- **SC-005**: Across 20 generation, cancellation, and failure trials, zero partial or generated files are left in an unchosen location or added to the shared library or generated-document history.
- **SC-006**: With external internet access unavailable and the workstation otherwise functioning, employees can complete the document-creation workflow without an external service; unavailable local generation is reported clearly and produces no file.
- **SC-007**: At least 90% of valid requests on a supported workstation result in a usable file or a clear, actionable failure message within 15 minutes.

## Assumptions

- The feature is for signed-in employees and administrators, consistent with the existing Ask and document-output surfaces.
- The employee explicitly chooses one of the four formats in the creation flow; format inference from the description is not required.
- Library material is optional. When a description requests or clearly depends on it, the system finds relevant material from the uploaded library; the employee does not have to select individual files in the first version.
- If no relevant library source is found, the employee may still create a document from the description, provided the result is clearly identified as not library-grounded.
- A document-derived factual statement must be traceable to its source; absent or conflicting library facts are disclosed rather than guessed.
- Generated files are transient system outputs. They are saved only to a location chosen by the employee and are not added to the library or document history.
- One request produces one document. In-app editing, custom templates, batch generation, and additional formats are outside this feature.
- The workstation remains responsible for generation and library use; the workflow must preserve the existing no-WAN and thin-client invariants.
- The existing Ask answer-export feature remains available and unchanged; this feature handles new documents composed from a description.

## Dependencies

- The existing signed-in employee and administrator experience.
- The existing uploaded-document library and its relevance and source attribution behavior.
- The existing supported document types and local workstation generation capability.

## Out of Scope

- Editing generated documents inside RAGGit.
- Adding generated documents to the shared library, indexing them, or retaining generated copies in history.
- Additional output types such as presentations, email sending, or image files.
- Batch or multi-document generation and custom document templates.
