# Feature Specification: Per-Person Identity & Accounts

**Feature Branch**: `004-identity`

**Created**: 2026-09-11

**Status**: Draft

**Input**: User description: "004-identity — real per-person accounts on the workstation (prerequisite for per-person query history)"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Operator provisions the first people (Priority: P1)

The person who installs the AI Workstation uses a local operator command to create accounts on the workstation itself (e.g. an Admin and an Employee). No in-app first-run wizard and no admin secret stored in configuration.

**Why this priority**: Establishes the identity authority before anyone can sign in; removes the bootstrap chicken-and-egg the project rejected.

**Independent Test**: Run the operator command on the workstation to add two accounts (one Admin, one Employee). Verify the accounts exist, are active, and their secrets are stored only as non-recoverable hashes; no first-run wizard is presented.

**Acceptance Scenarios**:

1. **Given** a fresh workstation with no accounts, **When** the operator runs the local provisioning command for an Admin and an Employee, **Then** both accounts exist and can be used to sign in.
2. **Given** a fresh workstation, **When** it starts with no accounts, **Then** it does NOT require or present an in-app "create admin" flow and stores no plaintext secret in configuration.

---

### User Story 2 - A person signs in and is identified (Priority: P1)

An employee opens the thin client on the LAN, signs in with username and password over HTTPS, and is thereafter identified as a specific person. Uploads and questions are attributed to that person, not to a shared key.

**Why this priority**: This is the core value — the library knows *who* acted.

**Independent Test**: Sign in as the provisioned Employee, upload a document, ask a question; verify the session identifies the person (role + display name) and both the document and query are attributed to that person's stable id.

**Acceptance Scenarios**:

1. **Given** a provisioned active account, **When** the person signs in with correct credentials over HTTPS, **Then** the session succeeds and the caller's identity is retrievable (identity type, role, display name).
2. **Given** an active session, **When** the person uploads a document or asks a question, **Then** the record is attributed to that person's stable identifier.
3. **Given** wrong credentials or an unknown username, **When** the person attempts to sign in, **Then** a single generic failure is returned that does not reveal whether the username exists.

---

### User Story 3 - Admin manages people in-app (Priority: P2)

An Admin opens the client's Admin screen (or uses the API) to list, create, deactivate, change the role of, or reset the password of accounts, and the change takes effect without restarting the workstation.

**Why this priority**: Completes the "admin-managed users table" decision and enables day-to-day stewardship without shell access.

**Independent Test**: As Admin, create a new Employee, change their role, deactivate them, and reset their password; verify each change is reflected immediately and a deactivated person is refused.

**Acceptance Scenarios**:

1. **Given** an Admin session, **When** the Admin creates a person with a role, **Then** that person can sign in with the assigned role.
2. **Given** an active person, **When** the Admin deactivates them, **Then** they cannot sign in and any existing session stops working.
3. **Given** an Employee session, **When** they attempt an Admin-only people action, **Then** the action is refused.

---

### User Story 4 - Client session lifecycle (Priority: P3)

A person signs in through the thin client, the session is cached, expiry prompts a re-sign-in, and signing out clears the credential.

**Why this priority**: Covers the thin client's session handling and thin-client security posture.

**Independent Test**: Sign in, kill the app process, relaunch — session is restored without re-typing. Let the token expire, attempt another action — the client re-prompts. Sign out — the cache is cleared and subsequent calls are anonymous.

**Acceptance Scenarios**:

1. **Given** a valid session, **When** the person closes and reopens the client, **Then** they remain signed in without re-entering credentials (until the token expires).
2. **Given** an expired session, **When** the person attempts an authenticated action, **Then** they are prompted to sign in again and no silent fallback to a shared key occurs.
3. **Given** a signed-in person, **When** they sign out, **Then** the cached credential is cleared and further requests are unauthenticated.

---

### Edge Cases

- Repeated failed sign-ins → account lockout after a configurable threshold, then refusal until reset or lockout expiry.
- Session expiry mid-use → client re-prompts for sign-in; no silent fallback to a shared key.
- Deactivation while a valid session is held → next request refused.
- Duplicate / username case handling → uniqueness enforced, case-insensitive matching.
- Admin password reset → optionally forces change at next sign-in.
- Client does not trust the workstation certificate (self-signed) → clear, actionable error; no insecure bypass.
- Workstation clock/timezone skew vs token lifetime → defined, bounded behavior.
- Documents/Queries attributed to legacy `"admin"/"employee"` shared-key identities → remain as-is; new actions use the stable person id.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST maintain a per-person account directory on the workstation with unique username, display name, role (Admin or Employee), and active/inactive status; the deployment remains single-tenant with no tenant/company dimension.
- **FR-002**: A person MUST authenticate with username and password over the LAN, and passwords MUST be stored only as a salted, non-recoverable hash.
- **FR-003**: On successful authentication the workstation MUST issue a short-lived, workstation-signed session credential identifying the person; the client MUST present it on subsequent requests and MUST re-authenticate when it expires.
- **FR-004**: The workstation operator MUST be able to create the first and additional accounts locally on the workstation (operator command) with no in-app first-run admin flow and no plaintext secret in configuration.
- **FR-005**: Administrators MUST be able to list, create, change role, deactivate, and reset the password of accounts via the API and a minimal in-app Admin screen.
- **FR-006**: The caller-identity endpoint MUST return an identity envelope including identity type, role, and display name, and MUST remain backward-compatible/additive with existing clients.
- **FR-007**: Uploads and queries MUST be attributed to the authenticated person's stable identifier; shared-key attribution MUST NOT be used for person actions.
- **FR-008**: Credentials and session tokens MUST traverse the LAN only over HTTPS.
- **FR-009**: Failed sign-ins MUST be rate-limited/locked out after a configurable number of attempts, and sessions MUST expire.
- **FR-010**: Deactivated accounts MUST NOT authenticate and MUST be refused on any existing session.
- **FR-011**: Authentication and authorization MUST resolve entirely on the workstation with no cloud identity egress at any time; when the provider is unavailable the system MUST fail fast with an offline-consistent error (no hang, no remote fallback).
- **FR-012**: The authentication provider MUST be configurable; API key remains valid for machine/bootstrap use and Windows AD remains a documented future provider, with local accounts as the per-person provider for this feature.
- **FR-013**: Identity contract changes MUST be additive (MINOR) with no breaking change to existing clients.

### Key Entities

- **Person Account**: a named person in the single-tenant directory — unique username, display name, role (Admin/Employee), active/inactive status, non-recoverable secret, last sign-in, created date.
- **Session Credential**: a short-lived, workstation-signed proof of a signed-in person — the person it identifies, role, and expiry.
- **Attribution**: the stable person identifier recorded on Documents and Queries.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A provisioned person can sign in and reach the library in under 10 seconds on the LAN.
- **SC-002**: 100% of uploads and queries made during a signed-in session are attributed to that person's stable identifier (zero shared-key attribution).
- **SC-003**: A deactivated account is refused on its very next request; zero further successful actions occur after deactivation.
- **SC-004**: On the wire, credentials and session tokens appear only inside HTTPS; no plaintext credential material is observable.
- **SC-005**: With WAN logically disabled, sign-in and a cited query still succeed end-to-end, and no cloud identity provider is contacted.
- **SC-006**: An Admin can create, change the role of, deactivate, or reset a person and the change takes effect without restarting the workstation.

## Clarifications

### Session 2026-09-11

- **Q1 - Identity provider**: Local per-person accounts on the workstation (Option B); Windows integrated/AD and API-key remain valid but are not the per-person provider here.
- **Q2 - Role assignment**: an admin-managed people directory (Users table) with roles Admin/Employee.
- **Q3 - Bootstrap / first admin**: no in-app first-run admin flow; the workstation operator provisions accounts locally (OS trust anchor). In-app Admin manages later.
- **Q4 - Session tokens**: framework-issued signed tokens (JWT bearer).
- **Q5 - Transport**: HTTPS required on the LAN for credentials and tokens.
- **Q6 - Scope**: identity and correct attribution only; per-person query history is a later feature.

## Assumptions

- The workstation is customer-owned; the operator has OS-level access and is the provisioning trust anchor.
- The thin client is the existing .NET MAUI app (Windows first; mobile deferred per `002` Q1); no local models or identity logic on the client beyond token cache.
- An internal/self-signed certificate is used for HTTPS; distributing trust to clients is an operational task.
- Single-tenant: all authenticated people share the one library for reading; only Admin manages documents and people. No per-user data isolation beyond attribution.
- Full SSO/OAuth/federation and Windows AD implementation are out of scope; AD remains documented as a future provider.
- Per-person query history is out of scope (later feature); 004 only establishes stable attribution and sign-in.

## Dependencies

- `002-real-bringup` (`v1.2.0`): the additive `{identityType, role}` envelope, thin-client session/handler, and configuration foundation that 004 extends.
- Governance: `Constitution VI` (auth provider wording) requires a MINOR amendment to add local per-person accounts as a provider — handled in this feature's `plan.md`.
