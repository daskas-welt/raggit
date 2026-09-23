# Quickstart: Dashboard Visual Refresh

**Feature**: `019-dashboard-visual-refresh` | **Date**: 2026-09-22 | **Plan**: [plan.md](plan.md) | **Contract**: [contracts/dashboard-page.md](contracts/dashboard-page.md)

Validation guide for the presentation-only shell and dashboard refresh. Implementation details belong in tasks; source data and state rules are in [data-model.md](data-model.md).

## Prerequisites

- .NET 10 SDK and Windows 10 1809+ / Windows 11 with WinUI 3 prerequisites.
- Workstation API available over LAN with representative documents, history, and at least one employee/admin account.
- Repo root: `C:\Users\mcaib\Documents\Projects\raggit`.
- Close Visual Studio or stop running API/client processes before rebuilding if file locks occur.

## Build and regression gates

```powershell
dotnet build src\RAGGit.Client.WinUI\RAGGit.Client.WinUI.csproj -p:Platform=x64
dotnet test --filter "FullyQualifiedName~Tests.Unit"
dotnet test --filter "FullyQualifiedName~Tests.Contract"
dotnet test --filter "FullyQualifiedName~Tests.Integration"
dotnet test --filter "QueryOfflineTests|OfflineIdentityTests|OfflineHistoryTests|CrossUserIsolationTests"
dotnet tool restore
dotnet csharpier check .
```

Expected: the WinUI build succeeds with zero warnings/errors, existing test suites remain green without assertion changes caused by this feature, the offline subset remains green without WAN, and formatting is clean.

## Manual walkthrough

1. Sign in as an employee. Confirm the app opens on Dashboard, shows the blue rail, context header, metric row, and primary panels, and does not show Admin.
2. Select Dashboard, Library, Ask, History, and My Docs. Confirm selected navigation is obvious and all existing stable navigation IDs still resolve.
3. Use Dashboard Ask and Dashboard Library actions. Confirm they reach the existing workflows rather than opening duplicate implementations.
4. Refresh with populated data. Confirm existing document/query counts and recent items are shown, loading is visible, duplicate refresh is prevented, and errors name the next step.
5. Repeat with no documents and no history. Confirm intentional empty states and usable next actions.
6. Sign in as an admin. Confirm Admin appears and remains gated; dashboard does not expose admin data to employees.
7. Submit a grounded query and open its result/detail. Confirm answer and citations remain prominent, selectable, and unchanged.

## Accessibility, themes, and responsive checks

- Keyboard only: navigate the rail, dashboard actions, metric links, and existing page controls in logical order; verify visible focus and Enter activation.
- Narrator: verify navigation selection, metric names/values, status announcements, empty states, and citation labels.
- Light, Dark, and High Contrast: verify rail, selected state, metric surfaces, text, borders, status, and focus remain distinguishable.
- Resize to 720px and below: verify navigation compacts/reflows, dashboard columns stack, no primary action is clipped, and collection controls retain their own scrolling.
- Seed 100+ documents/history items: verify collection scrolling remains smooth and no page-level `ScrollViewer` wraps a virtualized list.

## References

- Client surface contract: [contracts/dashboard-page.md](contracts/dashboard-page.md)
- Presentation state: [data-model.md](data-model.md)
- Design decisions: [research.md](research.md)
