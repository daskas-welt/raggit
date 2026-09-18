# Data Model: .NET 10 Upgrade (014-net10-upgrade)

**Date**: 2026-09-18

## Summary

This migration changes **no data shapes, stores, or API contracts**. There are no new entities, no modified fields, no state transitions, and no migrations. This document records the surfaces audited to prove that statement, so reviewers can verify "no data change" without re-deriving it.

## Audited surfaces (all unchanged)

| Surface | Location | Expected change |
|---------|----------|-----------------|
| Domain models (`Document`, `Chunk`, `Query`, `Library`, `User`, history DTOs) | `src/RAGGit.Core/Models/` | None — plain records, no runtime-coupled serialization |
| EF/persistence (`RagDbContext`, stores) | `src/RAGGit.Core/Data/` | None — provider versions unchanged |
| Vector/content stores | `src/RAGGit.Ingest/`, `src/RAGGit.Retrieval/` | None — storage paths and formats unchanged |
| Client models/VMs | `src/RAGGit.Client.Core/` | None — `CommunityToolkit.Mvvm` bump only on build failure (R-05) |
| HTTP contracts | `specs/*/contracts/api.yaml`, controllers in `src/RAGGit.Workstation.Api/Controllers/` | None — see `contracts/` note |
| MSIX identity (name, publisher, version scheme) | `src/RAGGit.Client.WinUI/Package.appxmanifest`, signing docs | None — package identity unchanged; only the build SDK changes |

## Validation rules carried over (unchanged)

- Single-tenant singleton Library (Constitution I) — untouched.
- Citation-grounded answers, `model unavailable offline` fail-fast (Constitution IV/V) — behavior verified by the unchanged offline suites, not re-specified here.

## Consistency audit list (FR-007 / SC-004)

Every item below MUST reference the new runtime after implementation; this list is the SC-004 audit checklist:

1. `global.json` (SDK pin → `10.0.401`)
2. `Directory.Build.props` (`net10.0`)
3. `src/RAGGit.Core/`, `RAGGit.Ingest/`, `RAGGit.Retrieval/`, `RAGGit.Client.Core/`, `RAGGit.Workstation.Api/` csproj targets (inherit `Directory.Build.props` — verify none override)
4. `tests/unit`, `tests/contract`, `tests/integration` csproj targets (explicit `net8.0` today — change each)
5. `src/RAGGit.Client.WinUI/RAGGit.Client.WinUI.csproj` (TFM + WindowsAppSDK `2.4.0` + Microsoft.Extensions `10.0.12`)
6. `src/RAGGit.Workstation.Api/Dockerfile` (`:10.0` tags)
7. `.github/workflows/ci.yml` (both `setup-dotnet` steps → `10.0.x`)
