[CmdletBinding()]
param(
    [ValidatePattern('^\d{4}-\d{2}$')]
    [string] $StartMonth = '2026-10',

    [ValidatePattern('^\d{4}-\d{2}$')]
    [string] $EndMonth = '2026-12',

    [string] $StateDirectory = 'artifacts/2026-q4-sync',

    [ValidateRange(1, 120)]
    [int] $ProbeIntervalMinutes = 15,

    [ValidateRange(1000, 10000)]
    [int] $RequestDelayMilliseconds = 1000
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runnerPath = Join-Path $PSScriptRoot 'run-available-race-data-tail.ps1'
$backfillPath = Join-Path $PSScriptRoot 'backfill-curated-result-participants.ps1'
$statePath = if ([System.IO.Path]::IsPathRooted($StateDirectory)) {
    [System.IO.Path]::GetFullPath($StateDirectory)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $StateDirectory))
}
$progressPath = Join-Path $statePath 'progress.csv'
$culture = [System.Globalization.CultureInfo]::InvariantCulture
$resultsPageUri = [uri]'https://www.britishhorseracing.com/racing/results/'
$apiProbeUri = [uri](
    'https://api09.horseracing.software/bha/v1/fixtures/' +
    '?resultsAvailable=1&fields=fixtureId&page=1&per_page=1')
$env:RaceDataSync__DelayBetweenRequestsMilliseconds =
    $RequestDelayMilliseconds.ToString($culture)

New-Item -ItemType Directory -Path $statePath -Force | Out-Null

function Get-PublicBhaToken {
    $page = Invoke-WebRequest -UseBasicParsing -Uri $resultsPageUri -TimeoutSec 30
    $appSource = [regex]::Matches(
        $page.Content,
        '<script[^>]+src=["'']([^"'']*\/angular\/app\.js[^"'']*)["'']',
        'IgnoreCase'
    ) | Select-Object -Last 1 | ForEach-Object { $_.Groups[1].Value }

    if ([string]::IsNullOrWhiteSpace($appSource)) {
        throw 'The official BHA Angular application script was not found.'
    }

    $appUri = [uri]::new($resultsPageUri, $appSource).AbsoluteUri
    $appScript = (Invoke-WebRequest -UseBasicParsing -Uri $appUri -TimeoutSec 30).Content
    $activeAuthLine = ($appScript -split "`n") | Where-Object {
        $_ -match '\$httpProvider\.defaults\.headers\.common.*Authorization' -and
        $_.TrimStart() -notmatch '^//'
    } | Select-Object -Last 1
    $tokenMatch = [regex]::Match($activeAuthLine, '(?i)Bearer\s+([^''";\s]+)')

    if (-not $tokenMatch.Success) {
        throw 'The official BHA results client did not expose an active bearer token.'
    }

    return $tokenMatch.Groups[1].Value
}

function Test-BhaApiReady([string] $Token) {
    $headers = @{
        Authorization = "Bearer $Token"
        Accept = 'application/json'
        Origin = 'https://www.britishhorseracing.com'
        Referer = $resultsPageUri.AbsoluteUri
    }

    try {
        $probe = Invoke-WebRequest `
            -UseBasicParsing `
            -Uri $apiProbeUri `
            -Headers $headers `
            -TimeoutSec 30
        return $probe.StatusCode -eq 200
    }
    catch {
        $statusCode = if ($_.Exception.Response) {
            [int] $_.Exception.Response.StatusCode
        }
        else {
            0
        }

        if ($statusCode -in @(418, 429)) {
            Write-Output (
                '{0:o} BHA API is throttled (HTTP {1}); waiting {2} minutes.' -f
                [datetime]::UtcNow,
                $statusCode,
                $ProbeIntervalMinutes)
            return $false
        }

        throw
    }
}

function Wait-ForExclusiveImportSlot {
    while ($true) {
        $otherImports = @(Get-CimInstance Win32_Process | Where-Object {
            $_.ProcessId -ne $PID -and
            -not [string]::IsNullOrWhiteSpace($_.CommandLine) -and
            ($_.CommandLine -match 'HorseRacing\.RaceDataSync' -or
                $_.CommandLine -match 'HorseRacing\.Bha\.CuratedPromoter' -or
                $_.CommandLine -match 'resume-after-throttle\.ps1' -or
                $_.CommandLine -match 'resume-monthly-race-data-history\.ps1' -or
                $_.CommandLine -match 'resume-available-race-data-tail\.ps1')
        })

        if ($otherImports.Count -eq 0) {
            return
        }

        Write-Output (
            '{0:o} Another import is active; waiting {1} minutes for the exclusive slot.' -f
            [datetime]::UtcNow,
            $ProbeIntervalMinutes)
        Start-Sleep -Seconds ($ProbeIntervalMinutes * 60)
    }
}

Wait-ForExclusiveImportSlot

$bhaToken = Get-PublicBhaToken
[Environment]::SetEnvironmentVariable(
    'BhaCollection__RacingStatusApiBearerToken',
    $bhaToken,
    'User')
$env:BhaCollection__RacingStatusApiBearerToken = $bhaToken

while (-not (Test-BhaApiReady -Token $bhaToken)) {
    Start-Sleep -Seconds ($ProbeIntervalMinutes * 60)
    $bhaToken = Get-PublicBhaToken
    $env:BhaCollection__RacingStatusApiBearerToken = $bhaToken
}

Write-Output (
    '{0:o} BHA API accepted the validation request; importing restartable Raw jobs at {1} ms/request, each followed by a Curated drain.' -f
    [datetime]::UtcNow,
    $RequestDelayMilliseconds)

& $runnerPath `
    -StartMonth $StartMonth `
    -EndMonth $EndMonth `
    -StateDirectory $statePath `
    -SkipBuild
if ($LASTEXITCODE -ne 0) {
    throw "The year-end result sync failed with exit code $LASTEXITCODE."
}

& $backfillPath
if ($LASTEXITCODE -ne 0) {
    throw "Participant backfill failed with exit code $LASTEXITCODE."
}

Write-Output ('{0:o} Available year-end results and participant links are current.' -f [datetime]::UtcNow)
