[CmdletBinding()]
param(
    [ValidatePattern('^\d{4}-\d{2}-\d{2}$')]
    [string] $ThroughDate,

    [ValidateRange(9000, 120000)]
    [int] $RequestDelayMilliseconds = 9000,

    [switch] $DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectPath = Join-Path $repositoryRoot 'src\HorseRacing.RaceDataSync'
$solutionPath = Join-Path $repositoryRoot 'HorseRacing.sln'
$culture = [System.Globalization.CultureInfo]::InvariantCulture
$londonTimeZone = [TimeZoneInfo]::FindSystemTimeZoneById('Europe/London')
$today = if ([string]::IsNullOrWhiteSpace($ThroughDate)) {
    [TimeZoneInfo]::ConvertTime([DateTimeOffset]::UtcNow, $londonTimeZone).Date
}
else {
    [datetime]::ParseExact($ThroughDate, 'yyyy-MM-dd', $culture)
}

$pastFrom = $today.AddDays(-7)
$pastTo = $today
$futureFrom = $today
$futureTo = $today.AddMonths(1)
$resultsPageUri = [uri]'https://www.britishhorseracing.com/racing/results/'
$apiProbeUri = [uri](
    'https://api09.horseracing.software/bha/v1/fixtures/' +
    ('?fields=fixtureId&year={0}&month={1}&page=1&per_page=1' -f
        $today.Year,
        $today.Month))

function Get-ActiveImportProcesses {
    return @(Get-CimInstance Win32_Process | Where-Object {
        $isWorker = $_.Name -match '^dotnet(?:\.exe)?$' -and
            ($_.CommandLine -match 'HorseRacing\.RaceDataSync' -or
                $_.CommandLine -match 'HorseRacing\.Bha\.CuratedPromoter')
        $isSupervisor = $_.Name -match '^pwsh(?:\.exe)?$' -and
            $_.CommandLine -match '-File\s+.*(?:resume-monthly-race-data-history|resume-available-race-data-tail|run-rolling-race-window)\.ps1(?:"|\s|$)'
        $_.ProcessId -ne $PID -and
        -not [string]::IsNullOrWhiteSpace($_.CommandLine) -and
        ($isWorker -or $isSupervisor)
    })
}

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

function Set-AndValidatePublicBhaToken {
    $token = Get-PublicBhaToken
    $client = [System.Net.Http.HttpClient]::new()
    $client.Timeout = [TimeSpan]::FromSeconds(30)
    $client.DefaultRequestHeaders.Authorization =
        [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $token)
    $client.DefaultRequestHeaders.Accept.ParseAdd('application/json')
    $client.DefaultRequestHeaders.UserAgent.ParseAdd(
        'HorseRacingLocalCollector/2.0 (+https://github.com/kelvindavies11/Horse-Racing)')
    [void] $client.DefaultRequestHeaders.TryAddWithoutValidation(
        'Origin',
        'https://www.britishhorseracing.com')
    $client.DefaultRequestHeaders.Referrer = $resultsPageUri
    $response = $null

    try {
        $response = $client.GetAsync($apiProbeUri).GetAwaiter().GetResult()
        $statusCode = [int] $response.StatusCode
    }
    finally {
        if ($null -ne $response) {
            $response.Dispose()
        }
        $client.Dispose()
    }

    if ($statusCode -ne 200) {
        throw "BHA API token validation returned HTTP $statusCode."
    }

    [Environment]::SetEnvironmentVariable(
        'BhaCollection__RacingStatusApiBearerToken',
        $token,
        'User')
    $env:BhaCollection__RacingStatusApiBearerToken = $token
    Write-Output 'The official BHA public token was refreshed and validated (HTTP 200).'
}

function Invoke-RaceDataSync([string] $Mode, [datetime] $From, [datetime] $To) {
    $arguments = @(
        'run',
        '--configuration',
        'Release',
        '--no-build',
        '--project',
        $projectPath,
        '--',
        '--mode',
        $Mode,
        '--from',
        $From.ToString('yyyy-MM-dd', $culture),
        '--to',
        $To.ToString('yyyy-MM-dd', $culture)
    )

    & dotnet @arguments
    if ($LASTEXITCODE -eq 0) {
        return
    }

    Write-Warning (
        '{0} collection exited {1}; refreshing the public token and retrying the unfinished work once.' -f
        $Mode,
        $LASTEXITCODE)
    Set-AndValidatePublicBhaToken
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Mode collection failed again with exit code $LASTEXITCODE."
    }
}

Write-Output (
    'Daily race window for {0}: results and details {1} through {2}; future details {3} through {4}.' -f
    $today.ToString('yyyy-MM-dd', $culture),
    $pastFrom.ToString('yyyy-MM-dd', $culture),
    $pastTo.ToString('yyyy-MM-dd', $culture),
    $futureFrom.ToString('yyyy-MM-dd', $culture),
    $futureTo.ToString('yyyy-MM-dd', $culture))

if ((Get-ActiveImportProcesses).Count -gt 0) {
    Write-Output 'Rolling collection skipped because another race-data import is active.'
    return
}

if ($DryRun) {
    Write-Output 'Dry run complete; the rolling collection is eligible and no network requests or database writes were made.'
    return
}

$connectionString = [Environment]::GetEnvironmentVariable(
    'ConnectionStrings__HorseRacing',
    'Process')
if ([string]::IsNullOrWhiteSpace($connectionString)) {
    $connectionString = [Environment]::GetEnvironmentVariable(
        'ConnectionStrings__HorseRacing',
        'User')
}
if ([string]::IsNullOrWhiteSpace($connectionString)) {
    $connectionString =
        'Host=localhost;Port=5433;Database=horse_racing;' +
        'Username=horse_racing;Password=horse_racing_local'
}

$env:ConnectionStrings__HorseRacing = $connectionString
$env:BhaCuratedPromotion__BatchSize = '1000'
$env:RaceDataSync__DelayBetweenRequestsMilliseconds =
    $RequestDelayMilliseconds.ToString($culture)
$env:RaceDataSync__ThrottleFallbackRequestDelayMilliseconds = '10000'

& dotnet build $solutionPath --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "The rolling collection build failed with exit code $LASTEXITCODE."
}

Set-AndValidatePublicBhaToken
Invoke-RaceDataSync -Mode 'all' -From $pastFrom -To $pastTo
Invoke-RaceDataSync -Mode 'upcoming' -From $futureFrom -To $futureTo

Write-Output 'Daily recent-results and future-race-details collection completed successfully.'
