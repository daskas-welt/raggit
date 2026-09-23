# Data Model: Dashboard Visual Refresh

**Feature**: `019-dashboard-visual-refresh` | **Date**: 2026-09-22 | **Plan**: [plan.md](plan.md)

No persistence, schema migration, API DTO, or server-owned entity is added. The dashboard is a transient presentation composition over existing client data.

## DashboardViewModel presentation state

| Field | Type | Meaning |
|-------|------|---------|
| `IsBusy` | `bool` | One or more dashboard summary loads are in flight; gates duplicate refresh and shows progress. |
| `StatusMessage` | `string?` | Plain-language loading, success, or failure outcome for the dashboard status surface. |
| `HasStatus` | `bool` | Derived state controlling status visibility. |
| `TotalDocuments` | `int` | Existing library total. |
| `ReadyDocuments` | `int` | Count of loaded documents whose existing status is `Ready`; labeled as loaded/current summary where completeness is limited. |
| `IndexingDocuments` | `int` | Count of loaded documents whose existing status is `Indexing`. |
| `FailedDocuments` | `int` | Count of loaded documents whose existing status is `Failed`. |
| `TotalQueries` | `int` | Existing history total, not a newly calculated estimate. |
| `MyDocuments` | `int` | Existing current-user document total when the source is available. |
| `RecentDocuments` | collection | Existing recent library items, bounded for dashboard display. |
| `RecentQueries` | collection | Existing recent history items, bounded for dashboard display. |
| `IsAdmin` | `bool` | Existing session role gate; controls admin-only navigation and content visibility. |
| `HasLibraryData` / `HasQueryData` | `bool` | Derived empty-state flags. |

The exact property names can be adjusted during implementation to match existing client naming, but no new server fields are implied.

## Existing source entities

### Document display

Existing fields used by the dashboard include filename, MIME/type, size, ingestion status, creator, and created timestamp. The dashboard may summarize these fields but does not own or mutate them.

### History item

Existing fields used by the dashboard include prompt preview, answer preview, citation count, latency, and created timestamp. Citation count and latency are shown only for the individual loaded item; no whole-history average is implied.

### Client session

Existing fields include role, identity type, workstation URL, and validity. The dashboard may show generic role/identity context but must not invent a username, display name, avatar, or initials because the current session object does not provide them.

## Validation and state transitions

```text
Unloaded → Loading → Loaded
                  → Empty
                  → Failed (status message, retry remains available)
Loaded/Empty → Loading (explicit refresh)
Any state → SessionExpired (existing session-expiry navigation path)
```

- All derived counts must come from existing loaded source data or an explicit existing total.
- Unsupported aggregate metrics are omitted rather than fabricated.
- Refresh must not clear usable prior data until replacement data is ready unless the existing page behavior already does so.
- Dashboard data is read-only; actions navigate to existing pages or invoke existing commands.
