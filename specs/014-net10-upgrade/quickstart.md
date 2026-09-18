# Quickstart / Validation Guide: .NET 10 Upgrade (014-net10-upgrade)

Proves the migration end-to-end. Run in order; each step gates the next. Expected outcomes double as the SC-001..SC-004 evidence.

## Prerequisites

- .NET SDK `10.0.401+` (`dotnet --list-sdks`), Windows 10 1809+ machine for client steps, Docker for step 5.

## 1. Pin check (SC-004)

```powershell
dotnet --version            # expect 10.0.4xx in repo root (global.json)
Select-String -Pattern 'net8\.0|"?8\.0\.' global.json, Directory.Build.props,
  src/*/RAGGit.*.csproj, tests/*/*.csproj,
  src/RAGGit.Workstation.Api/Dockerfile, .github/workflows/ci.yml
# expect: no matches (except historical notes in specs/)
```

## 2. Server build + tests — Linux or Windows (SC-001, SC-002)

```powershell
dotnet restore RAGGit.Server.slnf
dotnet build RAGGit.Server.slnf --no-restore -c Release   # 0 errors; warnings ≤ baseline
dotnet csharpier check .
dotnet test --filter "FullyQualifiedName~Tests.Unit" --no-build -c Release
dotnet test --filter "FullyQualifiedName~Tests.Contract" --no-build -c Release
# WAN-disabled offline suites + remaining integration per ci.yml
```

## 3. Full solution + MSIX gate — Windows only (SC-001, SC-003 prerequisite)

```powershell
dotnet restore RAGGit.sln
dotnet build RAGGit.sln --no-restore -c Release -p:Platform=x64
dotnet build src/RAGGit.Client.WinUI/RAGGit.Client.WinUI.csproj --no-restore -c Release `
  -p:Platform=x64 -p:AppxPackageSigningEnabled=false -p:GenerateAppxPackageOnBuild=true
# expect: bin/x64/Release/**/AppPackages/**/*.msix exists
```

## 4. WinUI smoke checklist — Windows, manual (SC-003)

Install the MSIX from step 3, then: sign in → library list + pagination → upload a document → ask a question (verify citations) → open history → admin people management. Zero regressions vs. pre-upgrade behavior.

## 5. Container (SC-001/SC-002 support)

```powershell
docker compose build raggit-workstation
# expect: builds on sdk:10.0, serves http://localhost:5001 (ASPNETCORE_URLS)
```

## 6. Consistency audit (SC-004)

Re-run the audit list in `data-model.md` (§Consistency audit list): all 7 items reference the new runtime; `git diff` shows no model, migration, controller, or manifest-identity changes.
