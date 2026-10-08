[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d{4}-\d{2}$')]
    [string] $StartMonth,

    [Parameter(Mandatory)]
    [ValidatePattern('^\d{4}-\d{2}$')]
    [string] $EndMonth,

    [Parameter(Mandatory)]
    [string] $StateDirectory,

    [ValidateRange(1, 120)]
    [int] $ProbeIntervalMinutes = 15,

    [ValidateRange(1000, 10000)]
    [int] $RequestDelayMilliseconds = 1000
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runnerPath = Join-Path $PSScriptRoot 'run-monthly-race-data-history.ps1'
$backfillPath = Join-Path $PSScriptRoot 'backfill-curated-result-participants.ps1'
$statePath = if ([System.IO.Path]::IsPathRooted($StateDirectory)) {
    [System.IO.Path]::GetFullPath($StateDirectory)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $StateDirectory))
}
$progressPath = Join-Path $statePath 'progress.csv'
$culture = [System.Globalization.CultureInfo]::InvariantCulture
$firstMonth = [datetime]::ParseExact($StartMonth, 'yyyy-MM', $culture)
$lastMonth = [datetime]::ParseExact($EndMonth, 'yyyy-MM', $culture)

if ($lastMonth -lt $firstMonth) {
    throw 'EndMonth must not be earlier than StartMonth.'
}

$monthCount = (($lastMonth.Year - $firstMonth.Year) * 12) +
    $lastMonth.Month - $firstMonth.Month + 1
$resultsPageUri = [uri]'https://www.britishhorseracing.com/racing/results/'
$apiProbeUri = [uri](
    'https://api09.horseracing.software/bha/v1/fixtures/' +
    '?resultsAvailable=1&fields=fixtureId&page=1&per_page=1')
$buildCompleted = $false
$script:BhaProbeRetrySeconds = $ProbeIntervalMinutes * 60

New-Item -ItemType Directory -Path $statePath -Force | Out-Null
$env:RaceDataSync__DelayBetweenRequestsMilliseconds =
    $RequestDelayMilliseconds.ToString($culture)

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
    $script:BhaProbeRetrySeconds = $ProbeIntervalMinutes * 60
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
        $responseProperty = $_.Exception.PSObject.Properties['Response']
        $statusCode = if ($null -ne $responseProperty -and $null -ne $responseProperty.Value) {
            [int] $responseProperty.Value.StatusCode
        }
        else {
            0
        }

        if ($statusCode -in @(418, 429)) {
            Write-Host (
                '{0:o} BHA API is still throttled (HTTP {1}); waiting {2} minutes.' -f
                [datetime]::UtcNow,
                $statusCode,
                $ProbeIntervalMinutes)
            return $false
        }

        if ($statusCode -in @(0, 401, 403)) {
            $script:BhaProbeRetrySeconds = 60
            Write-Host (
                '{0:o} BHA API readiness probe returned {1}; refreshing the public token and retrying in one minute.' -f
                [datetime]::UtcNow,
                $(if ($statusCode -eq 0) { 'no HTTP status' } else { "HTTP $statusCode" }))
            return $false
        }

        throw
    }
}

function Get-SucceededMonthCount {
    if (-not (Test-Path -LiteralPath $progressPath)) {
        return 0
    }

    return @(
        Import-Csv -LiteralPath $progressPath |
            Where-Object Status -eq 'Succeeded' |
            Select-Object -ExpandProperty From -Unique
    ).Count
}

function Wait-ForExclusiveImportSlot {
    while ($true) {
        $otherImports = @(Get-CimInstance Win32_Process | Where-Object {
            $isWorker = $_.Name -match '^dotnet(?:\.exe)?$' -and
                ($_.CommandLine -match 'HorseRacing\.RaceDataSync' -or
                    $_.CommandLine -match 'HorseRacing\.Bha\.CuratedPromoter')
            $isSupervisor = $_.Name -match '^pwsh(?:\.exe)?$' -and
                $_.CommandLine -match '-File\s+.*(?:resume-after-throttle|resume-monthly-race-data-history|resume-available-race-data-tail)\.ps1(?:"|\s|$)'
            $_.ProcessId -ne $PID -and
            -not [string]::IsNullOrWhiteSpace($_.CommandLine) -and
            ($isWorker -or $isSupervisor)
        })

        if ($otherImports.Count -eq 0) {
            return
        }

        Write-Output (
            '{0:o} Another historical import is active; waiting {1} minutes for the exclusive slot.' -f
            [datetime]::UtcNow,
            $ProbeIntervalMinutes)
        Start-Sleep -Seconds ($ProbeIntervalMinutes * 60)
    }
}

while ((Get-SucceededMonthCount) -lt $monthCount) {
    Wait-ForExclusiveImportSlot

    $bhaToken = Get-PublicBhaToken
    [Environment]::SetEnvironmentVariable(
        'BhaCollection__RacingStatusApiBearerToken',
        $bhaToken,
        'User')
    $env:BhaCollection__RacingStatusApiBearerToken = $bhaToken

    while (-not (Test-BhaApiReady -Token $bhaToken)) {
        Start-Sleep -Seconds $script:BhaProbeRetrySeconds
        $bhaToken = Get-PublicBhaToken
        $env:BhaCollection__RacingStatusApiBearerToken = $bhaToken
    }

    Write-Output (
        '{0:o} BHA API accepted the validation request; resuming restartable Raw jobs at {1} ms/request, each followed by a Curated drain.' -f
        [datetime]::UtcNow,
        $RequestDelayMilliseconds)

    if ($buildCompleted) {
        & $runnerPath `
            -StartMonth $StartMonth `
            -EndMonth $EndMonth `
            -StateDirectory $statePath `
            -SkipBuild
    }
    else {
        & $runnerPath `
            -StartMonth $StartMonth `
            -EndMonth $EndMonth `
            -StateDirectory $statePath
        $buildCompleted = $true
    }

    $runnerExitCode = $LASTEXITCODE
    $succeededMonths = Get-SucceededMonthCount
    Write-Output (
        '{0:o} Batch exited {1}; {2}/{3} months have succeeded.' -f
        [datetime]::UtcNow,
        $runnerExitCode,
        $succeededMonths,
        $monthCount)

    if ($succeededMonths -lt $monthCount) {
        Start-Sleep -Seconds ($ProbeIntervalMinutes * 60)
    }
}

& $backfillPath
if ($LASTEXITCODE -ne 0) {
    throw "Participant backfill failed with exit code $LASTEXITCODE."
}

Write-Output (
    '{0:o} All {1} monthly imports succeeded and participant identities were backfilled.' -f
    [datetime]::UtcNow,
    $monthCount)
