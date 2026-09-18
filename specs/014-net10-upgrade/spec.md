# Feature Specification: .NET 10 Upgrade

**Feature Branch**: `014-net10-upgrade`

**Created**: 2026-09-18

**Status**: Draft

**Input**: User description: "Upgrade to .NET 10"

**Decisions (user-confirmed)**: Timing — now; Windows App SDK — 2.x latest stable (breaking-change fixes in scope); Scope — entire repo at once (server, libraries, tests, WinUI client, Docker, CI) in a single workstream.

## Clarifications

### Session 2026-09-18

- Q: Which Windows App SDK line should the upgrade target? → A: Target 2.x latest stable, fixing any breaking changes in scope.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Workstation runs on the supported runtime (Priority: P1)

The operator deploys and runs the AI Workstation on the supported LTS runtime after .NET 8 end-of-support (~Nov 2026). All workstation behavior — document ingest, LAN query with citations, history, identity — works exactly as before with no operator action beyond normal deployment.

**Why this priority**: Without this, the product runs on an unsupported runtime within ~2 months; every other outcome depends on the runtime upgrade landing.

**Independent Test**: Deploy the workstation container/service from the upgraded build and run the full offline RAG flow (upload → query with citations → history) over LAN with WAN disabled.

**Acceptance Scenarios**:

1. **Given** a workstation built from the upgraded codebase, **When** an operator starts it and an employee uploads a document and asks a question over LAN with WAN disabled, **Then** the answer returns with source citations and the query appears in history.
2. **Given** the upgraded build, **When** CI runs the WAN-disabled offline integration suite, **Then** all offline suites pass (query, identity, history, cross-user isolation with 0 leaks).

---

### User Story 2 - Windows desktop client installs and runs unchanged (Priority: P1)

A Windows user (Windows 10 1809 or later, including Windows 11) installs the desktop client built from the upgraded codebase and performs daily work — sign in, browse the library with pagination, upload, ask questions with citations, view history, manage people (admin) — with no visible behavior change.

**Why this priority**: The desktop client is the employee's only interface; a runtime upgrade that breaks install or any screen is a failed upgrade.

**Independent Test**: Install the produced MSIX on the minimum supported OS generation and complete the smoke checklist (sign in → library → upload → ask with citations → history → pagination → admin) with no regressions.

**Acceptance Scenarios**:

1. **Given** the MSIX produced by the Windows packaging gate, **When** installed on a supported Windows version, **Then** installation succeeds and the app launches to sign-in.
2. **Given** a signed-in user, **When** they page the library, open a query, and an admin manages people, **Then** all screens behave as before the upgrade (same data, same outcomes).

---

### User Story 3 - CI and delivery stay green on both runners (Priority: P2)

Given a push or pull request after the upgrade, the Linux job (server build + unit/contract/offline/integration tests) and the Windows job (full solution build + unsigned MSIX packaging gate + artifact upload) both pass without special casing or skipped gates.

**Why this priority**: CI is the enforcement mechanism for every constitution gate (offline invariant, contracts, coverage); if CI is red or bypassed, the upgrade cannot be trusted.

**Independent Test**: Push the upgrade branch and observe both CI jobs green, including the MSIX artifact upload step.

**Acceptance Scenarios**:

1. **Given** the upgraded branch, **When** CI runs, **Then** the Linux job passes build, formatting check, unit, contract, WAN-disabled offline, and remaining integration tests.
2. **Given** the upgraded branch, **When** CI runs, **Then** the Windows job builds the full solution and uploads the MSIX artifact.

---

### Edge Cases

- What happens when the 2.x Windows App SDK introduces breaking API changes versus the pinned 1.5.x? Breaking-change fixes are in scope for this upgrade; the plan phase runs a scratch-project spike to pin the exact 2.x version before touching the client, and the selected version is recorded as an assumption update.
- How does the system handle third-party packages without a new major (e.g., extensions libraries, MVVM toolkit)? Compatible 8.x packages keep working on the new target; only packages that fail to restore/build are bumped, each recorded.
- What happens to developers still on the old SDK? The repo's SDK pin is updated so `dotnet` commands resolve to the new SDK; the old SDK is no longer required.
- What happens if the OllamaSharp source-generator version-skew warning changes behavior under the new compiler? The full test suite (unit + integration) is the detector; generator output changes are treated as failures to investigate, not warnings to suppress.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: All projects in the repo MUST build with the new SDK with zero errors, and pre-existing warnings MUST NOT increase.
- **FR-002**: The unit suite (228 tests), contract suite, and WAN-disabled offline integration suites MUST pass with no change in intent (RequiresOllama skips still honored).
- **FR-003**: The Windows client MUST build for x64 and produce an installable MSIX via the existing packaging-gate command.
- **FR-004**: The minimum supported OS for the desktop client MUST remain Windows 10 1809 — verified by install/run or manifest inspection.
- **FR-005**: The workstation container image MUST build and serve on port 5001 as before the upgrade.
- **FR-006**: CI MUST reference the new runtime consistently (Linux server job and Windows client job) with no job pinned to the old runtime.
- **FR-007**: The repo's SDK pin, shared build properties, and every per-project target MUST reference the new runtime consistently — no project left on the old target.
- **FR-008**: There MUST be no user-visible behavior change across login, library list/pagination, upload, query with citations, history, and admin screens (verified by the smoke checklist).

### Key Entities

- **Runtime pin files**: SDK pin, shared build properties, per-project targets — the consistency surface; a single stale pin fails FR-007.
- **CI pipelines**: Linux server job and Windows client-plus-MSIX job — the enforcement surface for FR-006 and the offline invariant.
- **Container definition**: base and SDK images — the deployment surface for FR-005.
- **Desktop package (MSIX)**: the installability artifact that proves FR-003/FR-004.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: CI is green on both runners (Linux + Windows) including the MSIX artifact upload step.
- **SC-002**: Full test results match or exceed the pre-upgrade baseline — 228/228 unit tests plus green contract, offline isolation (0 cross-user leaks), and remaining integration suites.
- **SC-003**: The manual WinUI smoke checklist (sign in → library → upload → ask with citations → history → pagination → admin) completes with zero regressions.
- **SC-004**: A consistency audit finds zero references to the old runtime as a build target across pins, projects, container definition, and CI configuration.

## Assumptions

- Portable runtime identifiers already in use — no RID-graph issues expected.
- New-language-version warnings will not fail builds (warnings-as-errors is off); pre-existing warnings are baselined, not fixed here.
- Compatible 8.x third-party packages keep working on the new target; only restore/build failures trigger a package bump, each recorded in the plan.
- The exact Windows App SDK 2.x version (latest stable at implementation time) is selected during planning via a scratch-project spike and recorded before client changes begin; 2.x supports back to Windows 10 1809, matching the OS floor.
- This is a runtime/targeting migration only: no data, schema, API-contract, or user-experience changes.
- Dependency: the constitution's Technology & Deployment Constraints and principle III reference the old runtime and will need a follow-up amendment (wording update) after this spec lands; historical specs are unaffected.
