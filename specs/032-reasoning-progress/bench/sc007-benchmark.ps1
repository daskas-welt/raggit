param(
    [string]$BaseUrl = "http://localhost:5144",
    [string]$EmployeeKey = "bench-employee",
    [int]$Runs = 6,
    [string[]]$Questions = @(
        "What is the refund policy?",
        "Within how many days may purchases be refunded?",
        "What does the document say about refunds?",
        "Which policy covers returns or refunds?",
        "How long is the refund window?",
        "Summarise the refund rules."
    ),
    [string]$OutFile = (Join-Path $PSScriptRoot 'sc007-results.json')
)

# SC-007 / SC-001 / SC-002 benchmark for feature 032.
#
# Compares the two transports on the SAME library with a warm model, alternating
# arms within each question so drift in machine/model state is shared:
#   classic = POST /api/queries        (the pre-032 path)
#   stream  = POST /api/queries/stream (the feature)
# Measured per run: wall time submit -> answer complete, the pipeline's own
# latencyMs, time to the first reasoning update (stream arm), and how many
# distinct updates arrived.

$ErrorActionPreference = 'Stop'
$progressStages = @('understanding', 'searching', 'sources', 'composing')

function New-Request {
    param([string]$Url, [string]$ApiKey, [string]$JsonBody)

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($JsonBody)
    $request = [System.Net.HttpWebRequest]::Create($Url)
    $request.Method = 'POST'
    $request.ContentType = 'application/json'
    $request.Headers.Add('X-Api-Key', $ApiKey)
    $request.ContentLength = $bytes.Length
    $request.Timeout = 300000
    $request.ReadWriteTimeout = 300000

    $stream = $request.GetRequestStream()
    $stream.Write($bytes, 0, $bytes.Length)
    $stream.Close()
    return $request
}

function Invoke-Classic {
    param([string]$Question)

    $body = @{ query = $Question; topK = 5 } | ConvertTo-Json -Compress
    $request = New-Request -Url "$BaseUrl/api/queries" -ApiKey $EmployeeKey -JsonBody $body

    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    $response = $request.GetResponse()
    $payload = (New-Object System.IO.StreamReader($response.GetResponseStream())).ReadToEnd()
    $watch.Stop()
    $response.Close()

    $parsed = $payload | ConvertFrom-Json
    return [pscustomobject]@{
        wallMs      = $watch.ElapsedMilliseconds
        serverMs    = $parsed.latencyMs
        firstLineMs = $null
        updates     = 0
        answer      = $parsed.answer
        citations   = @($parsed.citations).Count
    }
}

function Invoke-Stream {
    param([string]$Question)

    $body = @{ query = $Question; topK = 5 } | ConvertTo-Json -Compress
    $request = New-Request -Url "$BaseUrl/api/queries/stream" -ApiKey $EmployeeKey -JsonBody $body

    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    $response = $request.GetResponse()
    $reader = New-Object System.IO.StreamReader($response.GetResponseStream())

    $firstLineMs = $null
    $updates = 0
    $serverMs = $null
    $answer = ''
    $citations = 0

    while (($line = $reader.ReadLine()) -ne $null) {
        if ($line.Trim().Length -eq 0) { continue }
        if ($null -eq $firstLineMs) { $firstLineMs = $watch.ElapsedMilliseconds }

        $evt = $line | ConvertFrom-Json
        switch ($evt.type) {
            'progress' { if ($progressStages -contains $evt.stage) { $updates++ } }
            'answer' {
                $serverMs = $evt.latencyMs
                $answer = $evt.answer
                $citations = @($evt.citations).Count
            }
            'error' { throw "stream returned an error event: $($evt.error)" }
        }
    }

    $watch.Stop()
    $reader.Close()
    $response.Close()

    return [pscustomobject]@{
        wallMs      = $watch.ElapsedMilliseconds
        serverMs    = $serverMs
        firstLineMs = $firstLineMs
        updates     = $updates
        answer      = $answer
        citations   = $citations
    }
}

function Get-Median {
    param([double[]]$Values)
    $sorted = $Values | Sort-Object
    $mid = [int][math]::Floor($sorted.Count / 2)
    if ($sorted.Count % 2 -eq 1) { return $sorted[$mid] }
    return [math]::Round(($sorted[$mid - 1] + $sorted[$mid]) / 2, 1)
}

Write-Host "Warming the model (one discarded question per arm)..."
Invoke-Classic -Question $Questions[0] | Out-Null
Invoke-Stream -Question $Questions[0] | Out-Null

# NB: variable names are case-insensitive in PowerShell, so the accumulator must
# not be called $runs - that would collide with the [int]$Runs parameter.
$samples = @()
for ($i = 0; $i -lt ($Runs * 2); $i++) {
    $questionIndex = $i % $Questions.Count
    # Alternate which arm goes first within each question, so neither arm is
    # systematically penalised by a cold model or a busy machine.
    $streamFirst = [bool]($i % 2)
    $question = $Questions[$questionIndex]

    if ($streamFirst) {
        $streamResult = Invoke-Stream -Question $question
        $classicResult = Invoke-Classic -Question $question
    }
    else {
        $classicResult = Invoke-Classic -Question $question
        $streamResult = Invoke-Stream -Question $question
    }

    $samples += [pscustomobject]@{ arm = 'classic'; question = $question; wallMs = $classicResult.wallMs; serverMs = $classicResult.serverMs; firstLineMs = $null; updates = 0; citations = $classicResult.citations }
    $samples += [pscustomobject]@{ arm = 'stream'; question = $question; wallMs = $streamResult.wallMs; serverMs = $streamResult.serverMs; firstLineMs = $streamResult.firstLineMs; updates = $streamResult.updates; citations = $streamResult.citations }

    Write-Host ("  [{0}] classic {1,7} ms | stream {2,7} ms (first update {3,5} ms, {4} updates)" -f `
        $questionIndex, $classicResult.wallMs, $streamResult.wallMs, $streamResult.firstLineMs, $streamResult.updates)
}

$classicRuns = $samples | Where-Object { $_.arm -eq 'classic' }
$streamRuns = $samples | Where-Object { $_.arm -eq 'stream' }

$classicMedian = Get-Median -Values @($classicRuns.wallMs)
$streamMedian = Get-Median -Values @($streamRuns.wallMs)
$classicMean = [math]::Round(($classicRuns.wallMs | Measure-Object -Average).Average, 1)
$streamMean = [math]::Round(($streamRuns.wallMs | Measure-Object -Average).Average, 1)
$deltaPct = [math]::Round((($streamMedian - $classicMedian) / $classicMedian) * 100, 2)
$serverDeltaPct = [math]::Round(((Get-Median -Values @($streamRuns.serverMs)) - (Get-Median -Values @($classicRuns.serverMs))) / (Get-Median -Values @($classicRuns.serverMs)) * 100, 2)

$slowAnswers = @($streamRuns | Where-Object { $_.wallMs -gt 2000 })
$twoPlusUpdates = @($slowAnswers | Where-Object { $_.updates -ge 2 })
$withinOneSecond = @($streamRuns | Where-Object { $_.firstLineMs -le 1000 })

$summary = [pscustomobject]@{
    runsPerArm            = $Runs
    classicMedianMs       = $classicMedian
    streamMedianMs        = $streamMedian
    classicMeanMs         = $classicMean
    streamMeanMs          = $streamMean
    deltaPercentMedian    = $deltaPct
    deltaPercentServerMs  = $serverDeltaPct
    sc007Pass             = ($deltaPct -le 5)
    firstUpdateMedianMs   = Get-Median -Values @($streamRuns.firstLineMs)
    firstUpdateMaxMs      = ($streamRuns.firstLineMs | Measure-Object -Maximum).Maximum
    sc002WithinOneSecond  = "$($withinOneSecond.Count)/$($streamRuns.Count)"
    sc001SlowWithTwoPlus  = "$($twoPlusUpdates.Count)/$($slowAnswers.Count)"
    citationsAlwaysPresent = (@($samples | Where-Object { $_.citations -ge 1 }).Count -eq $samples.Count)
}

$summary | Format-List
$summary | ConvertTo-Json | Out-File $OutFile -Encoding UTF8
$samples | ConvertTo-Json | Out-File ($OutFile -replace '\.json$', '-runs.json') -Encoding UTF8
Write-Host "written: $OutFile"
