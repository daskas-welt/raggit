# Implementation Plan: WinUI 3 Client (Replace MAUI)

**Branch**: `010-winui-client` | **Date**: 2026-09-16 | **Spec**: [spec.md](./spec.md)

**Input**: Migrate the desktop client from .NET MAUI to WinUI 3 (Windows App SDK), replacing `RAGGit.Client.Maui` outright with `src/RAGGit.Client.WinUI`, reusing `RAGGit.Client.Core` unchanged. Windows-only; MSIX sideload for releases.

Scaffold already exists in the working tree (`src/RAGGit.Client.WinUI/`: csproj, `App.xaml(.cs)` DI, `MainWindow` + NavigationView, 5 platform adapters, `LoginPage`); this plan records decisions and drives remaining tasks.

## Summary

New WinUI 3 project `RAGGit.Client.WinUI` (`net8.0-windows10.0.17763.0`, `UseWinUI`, WindowsAppSDK 1.5 LTS) hosts the same Core ViewModels/ApiClients the MAUI shell used. Shell maps Flyout → `NavigationView` + `Frame`; views map `ContentPage` → `Page`, `CollectionView` → `ListView`/`ItemsRepeater`; five platform seams get WinUI adapters (PasswordVault, FileOpenPicker, Launcher, LocalSettings, DispatcherQueue navigator). Cutover deletes the MAUI project, drops the MAUI workload from docs/CI, amends the constitution (III/VII, v1.2.0 → v1.3.0), and ships signed MSIX in Release.

## Technical Context

**Language/Version**: C# .NET 8 (`global.json` SDK `8.0.425`)

**Primary Dependencies**: `Microsoft.WindowsAppSDK 1.5.240311000`, `CommunityToolkit.Mvvm 8.2.2` (via Core), `CommunityToolkit.WinUI.Controls.Segmented 8.1.240916` (pager/footer needs), `Microsoft.Extensions.{Configuration.Json,Configuration.UserSecrets,Http,DependencyInjection} 8.x`. **Removed**: `Microsoft.Maui.Controls 8.0.100`, `CommunityToolkit.Maui 9.1.1`, all MAUI workload TFMs.

**Storage**: None client-side (thin). Token in Credential Locker via `ISecureStorage` seam; URL/key from `appsettings.json` (+ user secrets in Debug); page size in LocalSettings.

**Testing**: Existing `tests/unit` (Core ViewModels/ApiClients, no changes), `tests/contract` + `tests/integration` (004/005 gates, no changes). New: smoke-test script for WinUI nav/auth/parity. Formatting: `dotnet csharpier check .`.

**Target Platform**: Windows 10 1809 (17763) + Windows 11, x64/x86/ARM64. Debug unpackaged (`WindowsPackageType=None`); Release MSIX (`/p:WindowsPackageType=MSIX`, dev-signed locally).

**Project Type**: Native Windows desktop shell (`RAGGit.Client.WinUI`) + unchanged shared library (`RAGGit.Client.Core`).

**Performance Goals**: Cold start to sign-in with no license/runtime prompt; history first page <2s p95 on LAN (unchanged); Ask ≥20 messages in order.

**Constraints**: Offline invariant NON-NEGOTIABLE (all calls LAN workstation URL; SDK assemblies compile-time bundled); thin client (FR-010); no API change (contract `1.4.0`); test-first for new adapter/page code.

**Scale/Scope**: 1 new project (net delete: MAUI removed), ~8 pages + 3 components ported, 5 adapters, DI/shell rewrite, constitution + docs update.

## Constitution Check

| Principle | Status | Evidence |
|-----------|--------|----------|
| **I. Single-Tenant On-Prem** | ✅ PASS | No tenant concept added; per-session client only. |
| **II. Workstation-Owned AI** | ✅ PASS | WinUI is UI + HttpClient; Core depends on `RAGGit.Core` DTOs + Mvvm only; FR-010. |
| **III. .NET Library-First & Client Reuse** | ⚠ AMEND | Principle names MAUI — this feature *implements* III via Core reuse but MUST amend III wording (MAUI → WinUI 3) as v1.3.0 MINOR. |
| **IV. Offline Invariant** | ✅ PASS | No runtime egress added; LAN-only; existing WAN-disabled suite unchanged. |
| **V. Citation-Grounded RAG** | ✅ PASS | Ask renders persisted answer + citations / no-content reply; no client generation. |
| **VI. Test-First** | ✅ PASS | Core tests unchanged + passing; new adapter/page code gets tests first; scaffold predates tasks → characterization via unchanged-suite green. |
| **VII. Simplicity & Proprietary Stewardship** | ⚠ JUSTIFIED | Net-zero projects (MAUI deleted, WinUI added — see Complexity Tracking); signed MSIX sideload directly serves VII distribution goal. |

*Gate: PASS with one MINOR amendment + one justified swap. Proceed.*

## Project Structure

```text
specs/010-winui-client/
├── plan.md / spec.md / tasks.md / checklists/requirements.md

src/
├── RAGGit.Client.WinUI/                  # NEW (replaces RAGGit.Client.Maui/)
│   ├── RAGGit.Client.WinUI.csproj        # net8.0-windows10.0.17763.0, UseWinUI, WinAppSDK 1.5
│   ├── app.manifest / appsettings.json
│   ├── App.xaml(.cs)                     # DI composition (mirrors MauiProgram valid-config path)
│   ├── MainWindow.xaml(.cs)              # NavigationView + Frame + config-error overlay
│   ├── Services/
│   │   ├── WinUISecureStorage.cs         # ISecureStorage over PasswordVault
│   │   ├── WinUIFilePicker.cs            # IFilePicker over FileOpenPicker (HWND)
│   │   ├── WinUILauncherService.cs       # ILauncherService over StorageFile+Launcher
│   │   ├── WinUILibraryPreferences.cs    # ILibraryPreferences over LocalSettings
│   │   └── WinUISessionExpiryNavigator.cs# ISessionExpirySink over DispatcherQueue
│   └── Views/                            # Login, Library, Query, History, QueryDetail,
│       └── *.xaml(.cs)                   # DocumentsMine, UploadSheet, Admin/Users
├── RAGGit.Client.Core/                   # UNCHANGED (ViewModels/services/session/config)
├── RAGGit.Client.Maui/                   # DELETED at cutover
tests/unit|contract|integration/           # UNCHANGED, must stay green
```

**Structure Decision**: Replace-in-place at `src/` level; WinUI keeps Core namespaces (`RAGGit.Client.Maui.*`) until an optional Polish rename — avoids churning Core + tests mid-migration.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Swap `RAGGit.Client.Maui` → `RAGGit.Client.WinUI` (VII project-count rule) | MAUI cross-platform shell cannot become WinUI: different SDK (`UseMaui` vs `UseWinUI`), XAML dialect, packaging, and platform APIs. Net-zero count (one removed, one added). Core reuse (III) preserved; Android/iOS explicitly dropped by owner decision. | (a) Retheme MAUI as "WinUI-like" — not WinUI 3, keeps MAUI runtime/workload, rejects the actual request. (b) Side-by-side permanently — two shells, duplicated XAML drift, violates VII simplicity. |
| WindowsAppSDK 1.5 dependency | Required runtime for WinUI 3 desktop apps; compile-time bundled, no query-time egress (IV safe). | No WinUI without it. |

## Post-Design Constitution Re-check

| Principle | Status | Post-design evidence |
|-----------|--------|----------------------|
| I / II / IV / V | ✅ PASS | Unchanged from pre-design check. |
| III | ⚠ AMEND | spec Assumptions + tasks Phase 6 record the v1.2.0 → v1.3.0 MINOR amendment (MAUI → WinUI 3 wording). |
| VI | ✅ PASS | tasks.md keeps Core suites green as US1–US3 checkpoints; new code test-first. |
| VII | ⚠ JUSTIFIED | Cutover phase deletes MAUI; Release MSIX serves private distribution. |

*Design gate: PASS. Ready for tasks.*
