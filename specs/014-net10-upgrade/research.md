# Research: .NET 10 Upgrade (014-net10-upgrade)

**Date**: 2026-09-18
**Method**: Local SDK/runtime inventory + NuGet flat-container version queries + Microsoft Learn docs. No code changes were made during research.

## R-01: .NET SDK pin

- **Decision**: Pin `global.json` to `10.0.401` with `rollForward: latestFeature` (unchanged policy).
- **Rationale**: `10.0.401` is installed locally alongside `8.0.425`; `latestFeature` stays inside the `10.0.4xx` feature band, matching the repo's existing pinning convention. Runtimes `10.0.12` (Core, ASP.NET Core, WindowsDesktop) are present.
- **Alternatives considered**: Floating `10.0.x` without exact version (rejected — non-reproducible); `latestMajor` roll-forward (rejected — would silently jump to .NET 11 previews).

## R-02: Target frameworks

- **Decision**: `Directory.Build.props` → `net10.0`; all test projects (`unit`, `contract`, `integration`) → `net10.0`; WinUI client → `net10.0-windows10.0.17763.0` (minimum-OS version unchanged).
- **Rationale**: Single consistent target (FR-007); keeping `17763` in the WinUI TFM preserves the Windows 10 1809 floor (FR-004). Portable RIDs (`win-x64;win-x86;win-arm64`) are already in use, so the .NET 8+ portable-RID graph trap (`NETSDK1083`) does not apply.
- **Alternatives considered**: Bumping the Windows SDK number in the TFM (e.g. `26100`) for a broader compile-time surface (rejected — changes analyzer behavior with no user benefit for a no-behavior-change migration; Microsoft guidance allows compiling against the existing floor).

## R-03: Windows App SDK version (clarified: 2.x latest stable)

- **Decision**: Pin `Microsoft.WindowsAppSDK` to **`2.4.0`**; re-check for a newer proven stable at implementation time.
- **Rationale**: `2.4.0` (2026-08-13) is the documented stable on Microsoft Learn with release notes and ~173k downloads. `2.5.1` appeared 2026-09-16 with ~0 downloads — too fresh to trust for a no-behavior-change migration. 2.x supports back to Windows 10 1809, matching the OS floor. The Windows build + MSIX packaging gate is the spike that validates the choice.
- **Alternatives considered**: `2.5.1` (rejected — unproven); `1.8.x` servicing (rejected by user decision — latest stable requested); staying on `1.5.240311000` (rejected — two major lines behind, unlikely to support `net10.0` targets).

## R-04: Microsoft.Extensions.* packages

- **Decision**: Bump `Microsoft.Extensions.Configuration.Json`, `Microsoft.Extensions.Configuration.UserSecrets`, `Microsoft.Extensions.Http` from `8.0.1` → **`10.0.12`** (latest 10.0.x servicing at research time).
- **Rationale**: Same release train as the runtime; verified to exist on NuGet. 8.x packages would restore on `net10.0`, but mixing major trains invites binding/analyzer drift.
- **Alternatives considered**: Keep `8.0.1` (rejected — violates FR-007 consistency spirit for first-party framework packages).

## R-05: CommunityToolkit.Mvvm

- **Decision**: Keep `8.2.2` unless restore/build fails on `net10.0`; fallback `8.4.2` (latest stable).
- **Rationale**: Minimal-bump policy from the spec; the toolkit targets down-level TFMs and works on newer runtimes. `8.4.2` is the verified fallback.
- **Alternatives considered**: Proactive bump to `8.4.2` (rejected — no failure evidence; generator changes risk churn in ViewModels).

## R-06: OllamaSharp

- **Decision**: Keep `5.4.30` (already latest stable per NuGet).
- **Rationale**: No newer version exists. The pre-existing `CS9057` analyzer-vs-compiler version-skew warning is baselined; under the .NET 10 SDK's newer Roslyn it may disappear — record the before/after warning count, treat any *new* warning as a failure to investigate (spec edge case).

## R-07: C# language version

- **Decision**: No `LangVersion` pin; accept the .NET 10 SDK default (C# 14).
- **Rationale**: `TreatWarningsAsErrors` is `false`, so new-version warnings cannot fail builds; FR-001's "warnings MUST NOT increase" is enforced by comparing warning counts before/after.

## R-08: Docker images

- **Decision**: `mcr.microsoft.com/dotnet/aspnet:10.0` and `mcr.microsoft.com/dotnet/sdk:10.0`.
- **Rationale**: .NET 10 LTS tags exist on MCR; same shape as current `8.0` Dockerfile. Verified by `docker build` of the workstation image.

## R-09: CI runners

- **Decision**: `actions/setup-dotnet@v4` with `dotnet-version: '10.0.x'` in both the Linux and Windows jobs.
- **Rationale**: `setup-dotnet` supports the 10.0.x channel; no workflow-logic change needed. Windows job still builds with `-p:Platform=x64` (AnyCPU fails SelfContained mode — pre-existing constraint, unchanged).

## Open spike (implementation phase, Windows runner)

- Build `RAGGit.Client.WinUI` with `net10.0-windows10.0.17763.0` + WindowsAppSDK `2.4.0`; run the unsigned-MSIX packaging gate. If `2.4.0` fails for SDK reasons (not app-code reasons), re-check NuGet for a newer proven 2.x stable and record the change; app-code breakages from the 1.5→2.x jump are fixed in scope per clarification.
