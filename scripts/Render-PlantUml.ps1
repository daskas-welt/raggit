#Requires -Version 5.1
<#
.SYNOPSIS
  Renders PlantUML .puml sources to .png (same directory).
.DESCRIPTION
  Finds plantuml.jar via $env:PLANTUML_JAR or ./tools/plantuml.jar, else prints
  install hints (choco install plantuml / manual jar download). No network at
  query time — diagrams are build-time artifacts.
  SVG outputs in docs/images/ are committed presentation artifacts; this script
  is the optional PNG path (needs Java). Commit source + renders.
.EXAMPLE
  powershell -File scripts/Render-PlantUml.ps1
  powershell -File scripts/Render-PlantUml.ps1 -Sources docs/architecture.puml
#>
param(
  [string[]]$Sources = @(
    'docs/architecture.puml',
    'docs/query-sequence.puml',
    'specs/004-identity/docs/architecture.puml',
    'specs/004-identity/docs/workflow.puml',
    'specs/004-identity/docs/sequence.puml',
    'specs/004-identity/docs/dataflow.puml'
  )
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$jar = $env:PLANTUML_JAR
if ([string]::IsNullOrWhiteSpace($jar)) { $jar = Join-Path $root 'tools/plantuml.jar' }

if (-not (Test-Path -LiteralPath $jar)) {
  Write-Warning "plantuml.jar not found at '$jar'."
  Write-Host 'Install one of:'
  Write-Host '  choco install plantuml   # puts plantuml.exe on PATH'
  Write-Host '  Download plantuml.jar from https://plantuml.com/download, save to ./tools/plantuml.jar or set $env:PLANTUML_JAR'
  Write-Host '  VS Code extension: jebbs.plantuml (preview without commit)'
  exit 2
}

if (-not (Get-Command java -ErrorAction SilentlyContinue)) {
  throw 'Java not found on PATH. Install a JRE to render PNG files (see docs/images/README.md).'
}
$images = Join-Path $root 'docs/images'
foreach ($rel in $Sources) {
  $src = Join-Path $root $rel
  if (-not (Test-Path -LiteralPath $src)) { Write-Warning "Skip missing $rel"; continue }
  Write-Host "-- $rel"
  & java -jar $jar -tpng $src
  if ($LASTEXITCODE -ne 0) { throw "PlantUML failed for $rel (exit $LASTEXITCODE)" }
}
# Presentation set: copy PNGs next to docs/images/ SVGs (sequence renders
# as sequence.png, so rename to identity-sequence.png to match the SVG set).
$presentation = @{
  'docs/architecture.png'                  = 'architecture.png'
  'docs/query-sequence.png'                = 'query-sequence.png'
  'specs/004-identity/docs/sequence.png'   = 'identity-sequence.png'
}
foreach ($from in $presentation.Keys) {
  $src = Join-Path $root $from
  if (Test-Path -LiteralPath $src) {
    Copy-Item -LiteralPath $src -Destination (Join-Path $images $presentation[$from]) -Force
    Write-Host "copied $from -> docs/images/$($presentation[$from])"
  }
}
Write-Host 'Done. Canonical renders are SVG in docs/images/ (via MCP); PNGs there are optional.'
