# Quickstart validation script for RAGGit Offline-Mode.
# Run from the repository root after installing the .NET 8 SDK.

$ErrorActionPreference = "Stop"

Write-Host "==> Validating RAGGit quickstart steps..." -ForegroundColor Cyan

Write-Host "`n[1/4] dotnet build" -ForegroundColor Cyan
dotnet build RAGGit.sln -c Release
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }

Write-Host "`n[2/4] unit tests" -ForegroundColor Cyan
dotnet test --filter "FullyQualifiedName~Tests.Unit" -c Release --no-build
if ($LASTEXITCODE -ne 0) { throw "unit tests failed" }

Write-Host "`n[3/4] contract tests" -ForegroundColor Cyan
dotnet test --filter "FullyQualifiedName~Tests.Contract" -c Release --no-build
if ($LASTEXITCODE -ne 0) { throw "contract tests failed" }

Write-Host "`n[4/4] integration tests (offline suite)" -ForegroundColor Cyan
dotnet test --filter "FullyQualifiedName~Tests.Integration" -c Release --no-build
if ($LASTEXITCODE -ne 0) { throw "integration tests failed" }

Write-Host "`n==> Quickstart validation passed." -ForegroundColor Green
Write-Host "Next steps: start the workstation API, run a WAN-disabled query from a client," -ForegroundColor Green
Write-Host "and verify SC-002 p95 <7s on the reference hardware." -ForegroundColor Green
