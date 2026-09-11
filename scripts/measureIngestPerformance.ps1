# Measures SC-001 ingest performance: 50-page PDF → Ready must be <300s (prod) / <4s fake gate.
# Usage: pwsh ./scripts/measureIngestPerformance.ps1
# Requires: dotnet 8 SDK, no Ollama needed (uses fakes). Real timing logged via RealLoopTests when Ollama present.

$ErrorActionPreference = "Stop"

Write-Host "==> SC-001 ingest performance gate (<300s, fake <4s)" -ForegroundColor Cyan
Write-Host "Running IngestPerformanceTests.Ingest_50PagePdf_MustBeFast_SC001 ..." -ForegroundColor Gray

$sw = [System.Diagnostics.Stopwatch]::StartNew()
dotnet test tests/integration/RAGGit.Tests.Integration.csproj --filter "Ingest_50PagePdf" --logger "console;verbosity=detailed" 2>&1 | Tee-Object -Variable testOutput
$sw.Stop()

if ($LASTEXITCODE -ne 0) {
    Write-Host "FAIL: ingest performance test failed (SC-001 >300s or fake gate >4s)" -ForegroundColor Red
    $testOutput | Out-String | Write-Host
    exit 1
}

Write-Host "`nWall time total $($sw.Elapsed.TotalSeconds.ToString("F2"))s" -ForegroundColor Green
Write-Host "SC-001 PASS: 50-page PDF ingest Ready <300s (fake gate <4s). Real Ollama timing measured separately via dotnet test --filter RequiresOllama (RealLoopTests, 300s budget)." -ForegroundColor Green
Write-Host "Record wall-clock in specs/002-real-bringup/verification.md (SC-001) with dev laptop specs." -ForegroundColor Gray
Write-Host "Note: <7s on reference workstation (SC-002) is production assumption to verify at deploy." -ForegroundColor Gray
