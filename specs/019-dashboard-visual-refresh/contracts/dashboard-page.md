# Contract: Dashboard Page Surface

**Feature**: `019-dashboard-visual-refresh` | **Date**: 2026-09-22 | **Plan**: [plan.md](../plan.md)

No HTTP or server contract changes. This contract documents the client ViewModel-to-View surface and shell behavior.

## Shell

```text
MainWindow
├── NavigationView (left on desktop, compact/top behavior at <=720px)
│   ├── Dashboard (new landing destination, stable AutomationId NavDashboard)
│   ├── Library (existing AutomationId NavLibrary)
│   ├── Ask (existing AutomationId NavAsk)
│   ├── History (existing AutomationId NavHistory)
│   ├── My Docs (existing AutomationId NavMyDocs)
│   └── Admin (existing AutomationId NavAdmin, admin-only)
└── ContentFrame
```

Existing navigation IDs MUST remain unchanged. The authenticated startup destination changes from Library to Dashboard; direct navigation to Library remains available.

## Dashboard page

```text
DashboardPage
├── Page header: generic welcome/context + existing role context
├── Metric row: existing document/query/personal counts only
├── Primary content panels: recent documents, recent queries, ingestion/status summary
├── Empty states: actionable existing Ask/Library/upload routes where permitted
└── Status surface: loading/error/session outcomes, accessible live announcement
```

New interactive elements MUST have stable Automation IDs, including at minimum:

- `NavDashboard`
- `DashboardRefreshButton`
- `DashboardAskButton`
- `DashboardLibraryButton`
- `DashboardMetricDocuments`
- `DashboardMetricQueries`

Metric tiles that are not interactive should expose meaningful names and values but must not pretend to be buttons.

## ViewModel rules

- `DashboardViewModel` composes existing client API/ViewModel data and stays UI-framework-free.
- Loading is explicit and duplicate refresh is gated by `IsBusy`.
- Failures use the existing plain-language client error/session-expiry path.
- Admin-only content follows `ClientSession.IsAdmin`; no ordinary employee sees admin summaries or navigation.
- Dashboard actions navigate to existing pages or invoke existing commands; they do not add alternate upload/query workflows.

## Visual and accessibility rules

- Semantic resources cover Light, Dark, and High Contrast; usage sites use `{ThemeResource}`.
- Blue navigation treatment is a purpose-based theme resource, not a literal color in page XAML.
- Every interactive element has an accessible name, visible focus, and at least the established 44px target.
- Existing automation IDs, citation presentation, `InfoBar` status semantics, and list-owned scrolling remain compatible.
