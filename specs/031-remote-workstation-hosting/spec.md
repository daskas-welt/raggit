# Feature Specification: Remote Workstation Hosting

**Feature Branch**: `031-remote-workstation-hosting`

**Created**: 2026-10-14

**Status**: Draft

**Input**: User description: "We need to host the workstation on a DigitalOcean VM and configure the desktop application to upload documents there and make queries there. REST API should run at the vm."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Reach the workstation from a desktop and sign in (Priority: P1)

An operator stands the AI Workstation up in one of two supported modes - on a machine on the company's own network (**local mode**), or on a company-controlled cloud machine reachable only over the company's private network (**private cloud mode**). In both modes the desktop application runs locally on the employee's PC and sends its API calls to that workstation. The employee signs in with their own account and lands on a working home screen, without any AI model, index, or library being installed on their PC.

**Why this priority**: Everything else depends on this. If an employee cannot reach and authenticate against the workstation, uploading and querying are impossible, so this is the minimum viable slice.

**Independent Test**: Can be fully tested by standing up the workstation in either mode, pointing one desktop client at it, and confirming a real account sign-in succeeds and the client shows a connected, signed-in state.

**Acceptance Scenarios**:

1. **Given** a workstation deployed in either mode, **When** an employee opens the desktop application configured with that workstation's address and their account credentials, **Then** they sign in successfully and see the signed-in home screen.
2. **Given** a client configured with the workstation address, **When** the employee performs any action, **Then** no AI model weights, vector index, or document store reside on the employee's PC.
3. **Given** the workstation is not reachable, **When** the employee attempts to sign in, **Then** the application shows a clear, actionable "cannot reach AI workstation" style message and does not hang or silently fall back to a local or hard-coded host.

---

### User Story 2 - Upload documents to the workstation (Priority: P1)

An employee uploads documents through the desktop application as they do today, but the files now travel to the workstation configured for this deployment, are indexed there by the workstation's background ingestion, and the employee watches each document move through its lifecycle to Ready (or Failed with a readable reason) while the original content only lives on the workstation.

**Why this priority**: Uploading the company's knowledge into the library is the first half of the product's value, and it is the explicit data-inflow the request names. It is independently demonstrable once connectivity (US1) exists.

**Independent Test**: Can be fully tested by uploading one or more documents from a desktop client to the workstation and confirming each reaches a settled Ready state and is visible in the library.

**Acceptance Scenarios**:

1. **Given** a signed-in employee on a desktop client pointed at the workstation, **When** they upload a supported document, **Then** the workstation accepts it and the document progresses through the existing lifecycle (Uploading, Queued, Indexing) to Ready.
2. **Given** an upload that cannot be extracted or exceeds limits, **When** it is submitted, **Then** it is accepted and later settles to Failed with a user-safe failure reason, matching current behavior.
3. **Given** the workstation has already indexed the document, **When** the employee queries it (see US3), **Then** the content is retrievable from that workstation's library.

---

### User Story 3 - Ask questions and receive cited answers from the library (Priority: P2)

An employee asks a question in the desktop application. The question goes to the configured workstation, which retrieves from its library and returns an answer with citations back to specific source chunks, or exactly "no relevant content found" when nothing matches.

**Why this priority**: This is the payoff the hosting enables, but it depends on documents having been uploaded first, so it sits just below connectivity and upload.

**Independent Test**: Can be fully tested by asking a set of questions whose answers are present in documents uploaded to the workstation and confirming each answer returns with correct citations, and that an uncovered question returns exactly "no relevant content found".

**Acceptance Scenarios**:

1. **Given** a library containing relevant content, **When** an employee asks a question, **Then** the answer includes citations to the source document and chunk on the workstation.
2. **Given** a question with no relevant content in the library, **When** the employee asks it, **Then** the response is exactly "no relevant content found" with no fabricated text.
3. **Given** the workstation is serving AI locally, **When** a query is answered, **Then** the answer requires no external AI service call.

---

### User Story 4 - Operate and maintain the workstation (Priority: P3)

The operator can check that the workstation is healthy in either mode, restart or reprovision it, take a restorable copy of the library, and tear it down when no longer needed - all without losing the company's committed documents, accounts, or configuration, and with visibility into what it costs.

**Why this priority**: Operations keep the deployment viable over time, but it is not required to demonstrate the core upload/query value, so it is lowest priority.

**Independent Test**: Can be fully tested by rebuilding or restoring the workstation from a saved copy and confirming the same accounts can sign in and the same documents remain queryable.

**Acceptance Scenarios**:

1. **Given** a healthy workstation, **When** the operator checks its health, **Then** they can determine from outside the machine whether the service is up and serving.
2. **Given** a populated workstation, **When** the operator saves a copy and later rebuilds the machine, **Then** all committed documents and accounts are preserved and remain usable.
3. **Given** the workstation is no longer needed, **When** the operator tears it down, **Then** no billable compute remains and the preserved library can still be restored later.

---

### Edge Cases

- What happens when the network link between the desktop and the workstation drops mid-upload or mid-query?
- What happens when the workstation's server certificate is not trusted by the employee's PC?
- What happens when the workstation's address changes (re-provisioning, address reassignment) after clients were configured?
- What happens when the machine restarts while documents are mid-ingestion?
- What happens when the workstation's storage fills up while uploads continue?
- What happens when an AI model required by the workstation is not present on the machine?
- What happens when many employees upload or query at the same time over the network link?
- What happens when the workstation is rebuilt from a saved copy that is older than the latest uploads?

## Requirements *(mandatory)*

### Deployment Modes

The workstation MUST support exactly two deployment modes. Employee desktops are local in both modes; only the workstation's location and reachability differ. The mode is selected per deployment and MUST NOT require source-code changes.

- **Local mode (default)**: The workstation runs on a machine on the company's own network. Desktops reach it over the local network. No cloud account is required, and all state stays on that machine.
- **Private cloud mode**: The workstation runs on a company-controlled cloud virtual machine. Desktops run locally and send their API calls to it over the company's private network (VPN and/or network access restricted to the company's own addresses). It is not exposed as an open public endpoint.

### Functional Requirements

- **FR-001**: The workstation (its REST API, its AI models, and its document library and metadata storage) MUST run entirely on a machine owned or controlled by the company - either on the company's own network (local mode) or on a company-controlled cloud virtual machine (private cloud mode) - and never on an employee desktop.
- **FR-002**: Employee desktops MUST remain thin clients - no AI model weights, vector index, or document store may reside on an employee PC.
- **FR-003**: The desktop application MUST be configurable to route all document uploads and all queries to a specified workstation address; once configured, it MUST NOT send uploads or queries anywhere else.
- **FR-004**: The desktop application MUST treat a missing or malformed workstation address, or a missing credential, as an invalid configuration and MUST NOT silently fall back to a local or hard-coded host.
- **FR-005**: All traffic between the desktop application and the workstation MUST be encrypted in transit, and credentials MUST NOT be transmitted in cleartext. In private cloud mode the workstation MUST be reachable only from the company's own networks (the company's LAN, its VPN, or source addresses the company controls) and MUST NOT be exposed as an open public endpoint.
- **FR-006**: An operator MUST be able to stand up the complete workstation on a fresh machine in either mode by following a repeatable, documented procedure that requires no source-code edits per deployment.
- **FR-007**: The workstation MUST durably persist the library content, document metadata, and accounts independently of the machine's lifecycle, so that replacing the machine preserves the company's data.
- **FR-008**: Employees MUST sign in to the workstation using the existing per-person account model; a machine/bootstrap credential remains valid for operator use.
- **FR-009**: A document uploaded from the desktop application MUST reach the workstation and progress through the existing ingestion lifecycle to Ready, or to Failed with a user-safe failure reason, and that outcome MUST be visible to the employee.
- **FR-010**: Queries MUST be answered from content hosted on the workstation, and every answer MUST include citations to the source document and chunk, or be exactly "no relevant content found".
- **FR-011**: The workstation MUST fulfil embedding, retrieval, and answer generation locally on the machine, without any query-time dependency on third-party AI services.
- **FR-012**: The operator MUST be able to determine from outside the machine whether the workstation service is up and serving.
- **FR-013**: The operator MUST be able to take a restorable copy of the workstation's committed data and to destroy the machine without losing that data.
- **FR-014**: When the workstation is unreachable, presents an untrusted certificate, or rejects the credential, the desktop application MUST show a clear, actionable error and MUST NOT hang.
- **FR-015**: Changing the workstation address or credential on an employee desktop MUST be possible by editing the desktop application's shipped configuration file, without rebuilding or reinstalling the application and without requiring a new in-app settings surface.
- **FR-016**: Only the workstation's client-facing service MUST be reachable, and only from the company's own networks; the AI/model service and any internal storage service MUST NOT be reachable from outside those networks in either mode.
- **FR-017**: The workstation MUST be single-tenant in both modes: one deployment serves exactly one company, with no tenant or company partitioning.
- **FR-018**: The system MUST support both deployment modes from the same codebase and the same documented procedure, so that switching a deployment from local to private cloud (or back) requires no source-code changes.

### Key Entities *(include if feature involves data)*

- **Deployment Mode**: The chosen topology - local (default) or private cloud - which determines where the workstation runs, how desktops reach it, and how its certificate is trusted. Employee desktops are local in both modes.
- **Workstation**: The single deployed instance of the RAG API, AI models, and library storage, running on a company-controlled machine (on-network or cloud virtual machine); the sole owner of the company's knowledge base.
- **Client Configuration**: The per-desktop settings that name the workstation address, provide the credential, and establish trust in the workstation's certificate; editable without rebuilding the app.
- **Library**: The company's single-tenant collection of ingested documents and their derived chunks, persisted on the workstation independently of the machine lifecycle.
- **Document**: A source file uploaded from a desktop, with a lifecycle state (Uploading/Queued/Indexing/Ready/Failed) and an optional failure reason, indexed on the workstation.
- **Account**: A per-person identity on the workstation used to sign in from a desktop, plus machine/bootstrap credentials for the operator.
- **Deployment Procedure**: The repeatable, documented steps that stand up the workstation on a fresh machine in either mode and configure it, containing no per-deployment code changes.
- **Data Snapshot**: A restorable copy of the workstation's committed library and account state, used to preserve data across machine rebuilds and teardown.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An operator can go from a fresh machine to a completed first successful employee sign-in in under 60 minutes, in either mode, by following the documented deployment procedure.
- **SC-002**: Once configured, 100% of desktop uploads and queries target the configured workstation, with zero local or hard-coded fallbacks.
- **SC-003**: An employee can upload a document from their desktop and then retrieve its content in an answer (grounded with a citation) end-to-end, using only the workstation.
- **SC-004**: 100% of answers are citation-grounded or return exactly "no relevant content found" - no ungrounded answers originate from the deployment.
- **SC-005**: After rebuilding or restoring the machine from a saved copy, 100% of previously committed documents and accounts remain usable, with no data loss.
- **SC-006**: When the workstation is unreachable or presents an untrusted certificate, the employee sees a clear, actionable error within 30 seconds instead of an indefinite hang.
- **SC-007**: Zero AI/model service ports (or other internal services) are reachable from outside the company's networks in either deployment mode.
- **SC-008**: The workstation sustains concurrent use by several employees (uploading and querying at once) without failed uploads or unanswered queries.
- **SC-009**: The documented teardown procedure leaves no billable compute running while preserving a restorable copy of the library.
- **SC-010**: Both deployment modes are stood up from the same codebase and documented procedure, with the mode selected by configuration only and no source-code change between them.

## Assumptions

- The company owns (or will create) the cloud account used in private cloud mode, and any hosting costs are borne by the company; local mode requires no cloud account. The deployment remains single-tenant (one company per workstation) in both modes.
- The desktop-to-workstation link may traverse a network the company does not fully control (for example a VPN over the public internet); the "no query-time cloud egress" invariant is interpreted as "the workstation answers entirely with its own locally hosted AI", not as "the transport cannot cross the internet".
- Existing RAG behavior is unchanged: chunking, retrieval, citations, the "no relevant content found" rule, ingestion lifecycle, and the per-person account model are reused as-is, so no retrieval or ingestion logic is re-specified here.
- The workstation defaults to the same model configuration used in development (the 1024-dimension embedding model and the configured chat model), and the same storage-volume sizing guidance applies.
- Employee PCs continue to run the existing Windows desktop application; mobile or browser clients are out of scope.
- Migrating an existing locally hosted library to private cloud mode is out of scope for the initial deployment (each mode starts with a fresh library at stand-up); only the deployment mechanism and client repointing are in scope.
- The operator has network access to the machine for setup and administrative actions, including access to the private network in private cloud mode.
- In private cloud mode the workstation is reachable only from the company's own networks (VPN and/or restricted source addresses) and is never left as an open public endpoint; the operator may later widen or narrow that boundary without changing the desktop application.
- Employee desktops receive the workstation address and credential through the application's shipped configuration file, edited per company by the operator; no new in-app configuration or settings screen is required.
- TLS trust on employee desktops is established as part of deployment, whether via a certificate issued by the company's own authority, a publicly trusted certificate, or a distributed server certificate.

## Dependencies

- Local mode: a machine on the company's network capable of running the workstation and its models and storage.
- Private cloud mode: cloud virtual machine provisioning, private networking (VPC/VPN), and network/firewall capability in the company's cloud account.
- Ability to install and run the AI model runtime, the workstation service, and its storage on the chosen machine.
- A durable, restorable storage location for the library and accounts (for example a mounted volume or a snapshot facility) so data survives machine rebuilds.
- The existing Windows desktop application and its configuration mechanism.
