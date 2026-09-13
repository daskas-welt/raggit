# Feature Specification: Per-Person Query & Document History

**Feature Branch**: `005-per-person-history`
**Created**: 2026-09-13
**Status**: Draft
**Input**: User description: "Start 005-per-person-history — each person sees own query history (recent queries with answers/citations), paginated, scoped to sub; admin sees own history only; identity required; offline WAN-off; includes per-person recent documents (CreatedBy) isolation check."
**Constitution**: v1.2.0 (Single-Tenant, Workstation-Owned AI, Offline WAN-off NON-NEGOTIABLE, Citation-Grounded, Test-First, Simplicity) — no amendment needed (query-layer addition over 004 attribution).

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Person views own query history (Priority: P1)

A signed-in employee opens the history screen and sees only their own past queries, each showing the prompt, answer, and citations. They can paginate through older entries and tap any entry to re-view the cited answer.

**Why this priority**: Core value — person can revisit previous answers without re-asking. Without this, 004 attribution is audit-only with no user payoff.

**Independent Test**: Sign in as Employee A, ask two questions, open history — verify exactly those two queries appear (no Employee B's queries). Sign in as Employee B, ask one question, open history — verify exactly one query and it is Employee B's.

**Acceptance Scenarios**:
1. **Given** a signed-in Employee with 3 prior queries, **When** they open the history screen, **Then** they see up to 3 entries ordered by most recent first, each with prompt text, answer snippet, and citation count.
2. **Given** a signed-in Employee with 0 prior queries, **When** they open the history screen, **Then** an empty-state message is shown (not an error) and the list is empty.
3. **Given** a signed-in Employee with 50 prior queries and a page size of 20, **When** they open history, **Then** the first 20 are shown; scrolling/tapping "next" loads the next 20; order is stable across requests.

---

### User Story 2 — Person views a past query with full answer and citations (Priority: P1)

A signed-in person taps a history entry and sees the full answer text and the list of citations (source document name, quoted text), identical to the original query response.

**Why this priority**: History without the answer is just a log — value is re-reading grounded answer and sources.

**Independent Test**: Ask a question that returns 2 citations, open history, tap that entry — verify full answer and both citations. Tap a different (older) entry — verify its answer and citations load independently.

**Acceptance Scenarios**:
1. **Given** a history entry with answer and 2 citations, **When** the person taps it, **Then** the full answer text and both citations (document reference, quoted text) are shown.
2. **Given** a history entry where the original query returned "no relevant content found", **When** the person taps it, **Then** the "no relevant content found" message is shown with zero citations.

---

### User Story 3 — Person views own recent documents (Priority: P2)

A signed-in employee sees a list of documents they personally uploaded (CreatedBy == person id), with filename, upload date, status, scoped to only their uploads.

**Why this priority**: Completes per-person audit surface — person sees both what they asked and what they contributed, without cross-user leakage.

**Independent Test**: Employee A uploads two documents, Employee B uploads one. Employee A sees only their two; Employee B sees only their one.

**Acceptance Scenarios**:
1. **Given** a signed-in Employee who uploaded 2 documents, **When** they open their recent documents, **Then** exactly those 2 appear with filename, CreatedAt, and status.
2. **Given** a signed-in Employee who uploaded 0 documents, **When** they open their recent documents, **Then** an empty list is shown (not an error).

---

### User Story 4 — Cross-user isolation is enforced (Priority: P1)

No person can see another person's query history or document uploads, regardless of role. An Admin sees only their own history — no cross-user view.

**Why this priority**: Security invariant — if isolation fails, feature is worse than useless. Must ship with first story.

**Independent Test**: Create two active accounts (Admin + Employee), each makes queries and uploads. Verify each sees only their own. Verify no API returns another user's rows.

**Acceptance Scenarios**:
1. **Given** two active persons (Admin, Employee) each with queries, **When** either opens history, **Then** only their own queries appear; zero rows from other person returned.
2. **Given** an expired or deactivated session, **When** person attempts to load history, **Then** request is refused (401/403) and no data returned.

---

### User Story 5 — History works offline (WAN logically off) (Priority: P2)

History loading works identically when the WAN is logically disabled — local-only over LAN.

**Why this priority**: Consistent with offline-first invariant (Constitution IV, 002 Q2).

**Independent Test**: Disable WAN (unshare -n fallback), sign in, ask a question, open history — verify query appears. Verify no external network call.

**Acceptance Scenarios**:
1. **Given** WAN logically disabled, **When** signed-in person opens history, **Then** own queries returned without delay/error.
2. **Given** WAN logically disabled, **When** signed-in person opens recent documents, **Then** own documents returned.

---

### Edge Cases

- Empty history: New user with zero queries sees empty state, not error.
- Deactivated account mid-browse: next page load refused (401 via OnTokenValidated).
- Legacy API-key queries (UserId = "admin"/"employee"): NOT visible in any person's history — excluded from per-person views.
- Page size at boundaries: limit=0 or limit>100 clamped to defaults (20 / 100).
- Offset beyond total: returns empty page with correct total count.
- Mixed legacy + person queries: person's history shows only UUID-attributed rows.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a paginated history endpoint returning only authenticated person's queries (filtered by UserId == JWT sub), ordered by CreatedAt DESC, Id DESC.
- **FR-002**: The history endpoint MUST accept limit (default 20, max 100) and offset (default 0) and return items (query summaries), total (count of person's queries), limit, offset.
- **FR-003**: Each history item MUST include Id, Prompt (preview), Answer (preview), CitationCount, LatencyMs, CreatedAt.
- **FR-004**: System MUST provide a query-detail endpoint returning full Answer, Citations array (document id, chunk id, text, ordinal), Prompt, LatencyMs, CreatedAt for a single query, scoped to authenticated person only (404 if not owned).
- **FR-005**: System MUST provide a paginated recent-documents endpoint returning only documents where CreatedBy == JWT sub, with Filename, Size, Status, CreatedAt.
- **FR-006**: Both history and documents endpoints MUST return 401 when no valid session credential is presented.
- **FR-007**: Both endpoints MUST NOT return rows belonging to any other person — zero cross-user leakage regardless of role (Admin or Employee).
- **FR-008**: Queries with legacy UserId values ("admin", "employee") MUST NOT appear in any person's history view.
- **FR-009**: History MUST work identically when WAN is logically disabled (local-only SQLite query).
- **FR-010**: Pagination MUST be stable — identical requests with same offset/limit return same items even if new queries inserted concurrently (sort key CreatedAt DESC, Id DESC).
- **FR-011**: The history endpoints MUST be additive (MINOR contract bump) with no breaking change to existing clients.
- **FR-012**: Admin persons see only their own history — no admin cross-user history view in v1.

### Key Entities

- **Query History View**: Filtered, paginated projection of existing Queries table, scoped to UserId == authenticated person's stable id. No new table; Queries already carries UserId from 004 attribution.
- **Recent Documents View**: Filtered, paginated projection of existing Documents table, scoped to CreatedBy == authenticated person's stable id. No new table; Documents already carries CreatedBy from 004 attribution.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A signed-in person with up to 1,000 queries can load their history page (first page) in under 2 seconds on LAN.
- **SC-002**: Over ≥2 active persons performing ≥20 mixed actions (queries + uploads), zero cross-user rows are returned by any history/documents endpoint — verified by automated test.
- **SC-003**: Pagination is consistent: requesting offset=0,limit=10 then offset=10,limit=10 yields two disjoint sets whose union equals the single-request limit=20 result set (same items, same order).
- **SC-004**: With WAN logically disabled, history and document endpoints return same data and latency as with WAN enabled — no external call, no degradation.
- **SC-005**: Legacy API-key-attributed queries (UserId = "admin"/"employee") do not appear in any person's history view — verified by automated test.

## Assumptions

- Relies entirely on 004's attribution: Queries.UserId and Documents.CreatedBy already populated with person's stable id for person sessions.
- No new tables/columns required — query-layer addition over existing data.
- Legacy shared-key queries (admin/employee) remain in DB but excluded from per-person views; no migration/deletion.
- No export (CSV/PDF) of history in v1; viewing only.
- No admin cross-user history view in v1; admins see only own history.
- Thin client (MAUI) will add a History tab/screen — UI scope not part of this spec; spec covers API contract + isolation invariant.
- Constitution v1.2.0 sufficient — query-layer read path, no auth change.

## Dependencies

- `004-identity` (v1.3.0): provides per-person accounts, JWT auth, Queries.UserId and Documents.CreatedBy attribution — required.
- `002-real-bringup` (v1.2.0): provides base API and configuration foundation.
