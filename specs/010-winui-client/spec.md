# Feature Specification: WinUI 3 Client (Replace MAUI)

**Feature Branch**: `010-winui-client`

**Created**: 2026-09-16

**Status**: Draft

**Input**: User description: "New feature: use WinUI 3 instead of MAUI"

**Decisions (confirmed 2026-09-16)**: Replace `RAGGit.Client.Maui` outright (no side-by-side) · Windows-only — drop `net8.0-android`/`net8.0-ios` opt-in builds · Packaging: MSIX packaged sideload for releases (double-click install on Win10 1809+/Win11), unpackaged F5 loop for Debug.

**Constitution**: v1.2.0 (Single-Tenant, Workstation-Owned AI, Offline WAN-off NON-NEGOTIABLE, Citation-Grounded, Test-First, Simplicity). Requires a MINOR constitution amendment (III: MAUI → WinUI 3; VII: project list `RAGGit.Client.Maui/` → `RAGGit.Client.WinUI/`).

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Person signs in and reaches the library on WinUI (Priority: P1)

A person launches the WinUI 3 app on Windows 10/11, signs in with their workstation account over HTTPS on the `LoginPage`, and lands on the Library. A still-valid cached session (Credential Locker) restores without prompting; an invalid workstation URL shows the configuration-error screen and never attempts HTTP.

**Why this priority**: Without sign-in + session restore there is no usable client — this is the MVP slice that proves DI, config, auth, and navigation end to end.

**Independent Test**: Fresh launch → sign in as Admin/Employee → Library shown; relaunch → session restored; corrupt `Workstation:Url` → config-error screen, no HTTP attempted.

**Acceptance Scenarios**:

1. **Given** a fresh install with valid config, **When** the person signs in with correct credentials, **Then** the Library page is shown and the role is discovered via `GET /api/auth/me`.
2. **Given** a cached unexpired session, **When** the app launches, **Then** it goes straight to the Library with no sign-in prompt.
3. **Given** an invalid workstation URL, **When** the app launches, **Then** a configuration-error screen is shown and no HTTP request is attempted.
4. **Given** wrong credentials, **When** sign-in is attempted, **Then** a clear error is shown and no session is cached.

---

### User Story 2 — Person uses all screens via side navigation (Priority: P1)

A signed-in person reaches Library, Ask (conversation with citations), History (+ detail), My Docs, Upload, and (admins only) Admin user management from a persistent `NavigationView` pane. All behavior comes from the shared `RAGGit.Client.Core` ViewModels/ApiClients; no screen implements its own server logic.

**Why this priority**: This is the behavior-parity slice — the whole reason the migration is safe is that Core is reused unchanged.

**Independent Test**: Sign in as Employee and Admin; walk all six destinations; verify Admin hidden for Employee; send two Ask questions and verify alternating messages with citations preserved.

**Acceptance Scenarios**:

1. **Given** a signed-in employee, **When** they open the nav pane, **Then** Library, Ask, History, My Docs, Upload are listed and reachable, Admin is hidden.
2. **Given** a signed-in admin, **When** they open the nav pane, **Then** Admin is also listed and opens user management.
3. **Given** the Ask page, **When** two questions are sent, **Then** each question and its answer+citations appear in order and earlier messages remain visible.
4. **Given** any screen, **When** it performs a server interaction, **Then** it uses the shared Core layer (no duplicate request/session code in WinUI).

---

### User Story 3 — Platform integrations behave natively (Priority: P2)

File upload uses `FileOpenPicker`; per-row download stages bytes and opens externally; library page size persists in LocalSettings; credential storage uses Credential Locker; mid-browse 401 returns to sign-in via `DispatcherQueue`.

**Why this priority**: The five seams are the only WinUI-specific code; everything else is reused. Each adapter is small and independently verifiable.

**Independent Test**: Upload a file via picker; download a document and verify external open; change page size, restart, verify persistence; force 401 mid-browse, verify return to sign-in.

**Acceptance Scenarios**:

1. **Given** the Upload page, **When** a file is picked, **Then** the OS picker opens and the selected file uploads (formats pdf/docx/xlsx/txt/md).
2. **Given** a library row with stored original, **When** download is tapped, **Then** bytes round-trip and the OS opens the file externally.
3. **Given** a changed library page size, **When** the app restarts, **Then** the size persists (invalid values fall back to 25).
4. **Given** an expired session mid-browse, **When** the next request returns 401, **Then** the token is cleared and the person is returned to sign-in.

---

### User Story 4 — MAUI is gone; users install via MSIX (Priority: P2)

`RAGGit.Client.Maui` is removed from the solution and deleted; `dotnet workload install maui` is gone from README/CI; the WinUI app installs via signed MSIX sideload on clean Win10 (1809+) and Win11 machines; README, constitution, and specs reference WinUI.

**Why this priority**: The cutover is what makes the migration real and prevents two-client drift.

**Independent Test**: Clean checkout builds with no MAUI workload; Release MSIX installs on Win10 1809 + Win11 VMs and runs offline-invariant flows.

**Acceptance Scenarios**:

1. **Given** a clean machine without the MAUI workload, **When** `dotnet build RAGGit.sln` runs, **Then** it succeeds.
2. **Given** the Release MSIX, **When** installed on Win10 1809 and Win11, **Then** the app launches to sign-in with no extra runtime install step.
3. **Given** the repo docs, **When** read, **Then** no MAUI references remain (README, constitution, CI, scripts).

---

### Edge Cases

- Missing/invalid config at startup: fail fast with actionable error, never blank-screen or crash.
- Empty lists: existing empty-state message, not an error.
- Non-admin navigating directly to admin: refused, destination stays hidden.
- Session expiry/deactivation mid-browse: next interaction refused, return to sign-in.
- Long Ask conversation: ≥20 messages preserved in order, scrollable.
- Legacy document with no stored original: "original unavailable", no crash.
- Win10 floor: binary targets `10.0.17763.0` so one package covers Win10 1809+ and Win11.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The WinUI app MUST reuse `RAGGit.Client.Core` ViewModels/ApiClients/session/config as its only behavior source — no duplicated server-interaction logic.
- **FR-002**: The app MUST target `net8.0-windows10.0.17763.0` (Windows App SDK) so one binary runs on Win10 1809+ and Win11.
- **FR-003**: Sign-in MUST be HTTPS-only (localhost exception for single-machine dev only), with cached-session restore and opportunistic refresh within 15 min of expiry.
- **FR-004**: Navigation MUST be a persistent `NavigationView` pane listing Library, Ask, History, My Docs, Upload, Admin (admin-only).
- **FR-005**: Credential storage MUST use Credential Locker (`PasswordVault`); tokens MUST NOT touch plain files or logs.
- **FR-006**: File pick MUST use `FileOpenPicker` (HWND-initialized); external open MUST stage to app-local storage first.
- **FR-007**: Library page size MUST persist in LocalSettings with 25 default and valid-option fallback (10/25/50/100).
- **FR-008**: Session expiry (401) MUST clear the token and return to sign-in on the UI thread.
- **FR-009**: Debug builds run unpackaged (F5 loop); Release builds package as signed MSIX for sideload.
- **FR-010**: The client MUST stay thin (no models/embeddings/vectors); all retrieval/generation stays on the workstation.
- **FR-011**: After cutover, existing `tests/unit` (Core), `tests/contract`, `tests/integration` MUST pass unchanged — no API contract change (still `1.4.0`).
- **FR-012**: No MAUI workload, package, or file may remain in the repo after cutover.

### Key Entities *(include if data involved)*

- **Client Session**: Unchanged from 006 — workstation URL, credential, role, admin capability (now hosted in WinUI DI).
- **WinUI Shell**: `MainWindow` + `NavigationView` + `Frame`; config-error overlay; admin visibility.
- **Platform Adapters**: Credential-Locker storage, file picker, launcher, LocalSettings prefs, DispatcherQueue expiry navigator — all behind existing Core seams.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All `tests/unit`, `tests/contract`, `tests/integration` pass with zero assertion changes.
- **SC-002**: Zero duplicated client server-interactions — each defined once in Core, referenced by WinUI pages.
- **SC-003**: Clean-checkout `dotnet build RAGGit.sln` succeeds with no MAUI workload installed.
- **SC-004**: All six destinations reachable in one nav selection; Admin hidden for non-admins in 100% of sessions.
- **SC-005**: Release MSIX installs and runs on Win10 1809 and Win11 with no extra runtime step.
- **SC-006**: Ask preserves ≥20 messages in order within a session.

## Assumptions

- `RAGGit.Client.Core` namespaces remain `RAGGit.Client.Maui.*` initially to avoid churn; an optional rename to `RAGGit.Client.Core.*`/`RAGGit.Client.*` is Polish, not required.
- Windows App SDK 1.5 LTS (`1.5.240311000`) — stable on .NET 8; 1.6 upgrade is a later option.
- No `MultiBinding` in WinUI — citation subtitle formatting moves to a converter or preformatted Core property.
- Constitution amendment (III + VII, v1.2.0 → v1.3.0 MINOR) ships in this feature with rationale + migration note.
- Dev cert signs local MSIX; production uses internal-CA/distributed cert per VII — operator-owned, out of scope for code tasks.
