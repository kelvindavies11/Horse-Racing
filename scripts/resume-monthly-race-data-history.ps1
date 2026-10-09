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

    [ValidateRange(10000, 120000)]
    [int] $RequestDelayMilliseconds = 10000,

    [ValidateRange(1, 5)]
    [int] $MaxParallelism = 1
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
$weatherProgressPath = Join-Path $statePath 'weather-progress.csv'
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
    ('?resultsAvailable=1&fields=fixtureId&year={0}&month={1}&page=1&per_page=1' -f
        $firstMonth.Year,
        $firstMonth.Month))
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
    $client = [System.Net.Http.HttpClient]::new()
    $client.Timeout = [TimeSpan]::FromSeconds(30)
    $client.DefaultRequestHeaders.Authorization =
        [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $Token)
    $client.DefaultRequestHeaders.Accept.ParseAdd('application/json')
    $client.DefaultRequestHeaders.UserAgent.ParseAdd(
        'HorseRacingLocalCollector/2.0 (+https://github.com/kelvindavies11/Horse-Racing)')
    [void] $client.DefaultRequestHeaders.TryAddWithoutValidation(
        'Origin',
        'https://www.britishhorseracing.com')
    $client.DefaultRequestHeaders.Referrer = $resultsPageUri
    $probe = $null
    $statusCode = 0

    try {
        $probe = $client.GetAsync($apiProbeUri).GetAwaiter().GetResult()
        $statusCode = [int] $probe.StatusCode
    }
    catch {
        $script:BhaProbeRetrySeconds = 60
        Write-Host (
            '{0:o} BHA API readiness probe returned no HTTP status; refreshing the public token and retrying in one minute.' -f
            [datetime]::UtcNow)
        return $false
    }
    finally {
        if ($null -ne $probe) {
            $probe.Dispose()
        }
        $client.Dispose()
    }

    if ($statusCode -eq 200) {
        return $true
    }

    if ($statusCode -in @(418, 429)) {
        Write-Host (
            '{0:o} BHA API is still throttled (HTTP {1}); waiting {2} minutes.' -f
            [datetime]::UtcNow,
            $statusCode,
            $ProbeIntervalMinutes)
        return $false
    }

    if ($statusCode -in @(401, 403)) {
        $script:BhaProbeRetrySeconds = 60
        Write-Host (
            '{0:o} BHA API readiness probe returned HTTP {1}; refreshing the public token and retrying in one minute.' -f
            [datetime]::UtcNow,
            $statusCode)
        return $false
    }

    throw "BHA API readiness probe returned unexpected HTTP $statusCode."
}

function Get-SucceededMonthCount {
    if (-not (Test-Path -LiteralPath $progressPath) -or
        -not (Test-Path -LiteralPath $weatherProgressPath)) {
        return 0
    }

    $rawMonths = @(
        Import-Csv -LiteralPath $progressPath |
            Where-Object Status -eq 'Succeeded' |
            Select-Object -ExpandProperty From -Unique
    )
    $weatherMonths = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::Ordinal)
    foreach ($from in @(
        Import-Csv -LiteralPath $weatherProgressPath |
            Where-Object Status -eq 'Succeeded' |
            Select-Object -ExpandProperty From -Unique)) {
        [void] $weatherMonths.Add($from)
    }

    return @($rawMonths | Where-Object { $weatherMonths.Contains($_) }).Count
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
        '{0:o} BHA API accepted the validation request; resuming with up to {1} Raw runners behind one shared {2} ms request gate, with periodic serialized Curated drains while Raw collection continues.' -f
        [datetime]::UtcNow,
        $MaxParallelism,
        $RequestDelayMilliseconds)

    if ($buildCompleted) {
        & $runnerPath `
            -StartMonth $StartMonth `
            -EndMonth $EndMonth `
            -StateDirectory $statePath `
            -MaxParallelism $MaxParallelism `
            -SkipBuild
    }
    else {
        & $runnerPath `
            -StartMonth $StartMonth `
            -EndMonth $EndMonth `
            -StateDirectory $statePath `
            -MaxParallelism $MaxParallelism
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
