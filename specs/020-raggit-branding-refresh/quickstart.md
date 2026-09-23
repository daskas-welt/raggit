# Quickstart: Raggit Branding Refresh

**Feature**: `020-raggit-branding-refresh` | **Date**: 2026-09-22 | **Plan**: [plan.md](plan.md) | **Contract**: [contracts/branding-shell.md](contracts/branding-shell.md)

Validation guide for the shell-only branding change. No implementation code belongs here.

## Prerequisites

- .NET 10 SDK and Windows 10 1809+ / Windows 11 with WinUI 3 prerequisites.
- Repository root: `C:\Users\mcaib\Documents\Projects\raggit`.
- Stop running Visual Studio/API/client processes or use Release output if Debug binaries are locked.

## Build and regression gates

```powershell
dotnet build src\RAGGit.Client.WinUI\RAGGit.Client.WinUI.csproj -c Release -p:Platform=x64
dotnet test --filter "FullyQualifiedName~Tests.Unit"
dotnet test --filter "FullyQualifiedName~Tests.Contract"
dotnet test --filter "Tests.Integration"
dotnet test --filter "QueryOfflineTests|OfflineIdentityTests|OfflineHistoryTests|CrossUserIsolationTests"
dotnet tool restore
dotnet csharpier check .
```

Expected: the WinUI Release build succeeds, existing regression suites remain green subject to documented repository prerequisites, no assertion changes are required, and formatting passes.

## Manual walkthrough

1. Launch an authenticated session in Light mode. Confirm `Raggit` appears at the top of the left navigation area, the rail is blue, and the workspace is light and readable.
2. Select Dashboard, Library, Ask, History, My Docs, and Admin where permitted. Confirm selection behavior and all existing Automation IDs remain unchanged.
3. Switch to Dark mode. Confirm the rail, `Raggit`, selected state, workspace, profile card, and focus indicators remain readable.
4. Switch to High Contrast. Confirm system colors preserve product-name visibility, selected state, focus, and navigation labels.
5. Resize to exactly 720px and below. Confirm navigation switches to Top mode, `Raggit` remains visible/reachable, and no destination or dashboard action is clipped.
6. Test employee and admin sessions. Confirm Admin visibility and all existing profile information remain unchanged.
7. Exercise configuration error, session expiry, upload dialog, grounded answer/citation, loading, empty, and error states. Confirm branding does not obscure or alter their semantics.
8. Seed 100+ documents/history entries and verify list-owned scrolling remains smooth with no new page-level scroll wrapper.

## References

- Client surface contract: [contracts/branding-shell.md](contracts/branding-shell.md)
- Presentation model: [data-model.md](data-model.md)
- Design decisions: [research.md](research.md)
