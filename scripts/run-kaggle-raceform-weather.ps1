[CmdletBinding()]
param(
    [Parameter()]
    [datetime]$From = [datetime]'2015-01-01',

    [Parameter()]
    [datetime]$To = [datetime]'2026-05-31',

    [Parameter()]
    [ValidateRange(250, 10000)]
    [int]$DelayMilliseconds = 250,

    [Parameter()]
    [ValidateRange(1, 10)]
    [int]$MaximumAttempts = 4
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = Join-Path $repositoryRoot 'src\HorseRacing.RaceDataSync\HorseRacing.RaceDataSync.csproj'
$stateDirectory = Join-Path $repositoryRoot 'artifacts\kaggle-raceform-weather'
$progressPath = Join-Path $stateDirectory 'progress.csv'
$culture = [System.Globalization.CultureInfo]::InvariantCulture

if ($To.Date -lt $From.Date) {
    throw 'To must be on or after From.'
}

$token = [Environment]::GetEnvironmentVariable(
    'BhaCollection__RacingStatusApiBearerToken',
    'User')
if ([string]::IsNullOrWhiteSpace($token)) {
    throw 'The documented public BHA client-token refresh must be completed before RaceDataSync is started.'
}

$env:BhaCollection__RacingStatusApiBearerToken = $token
$env:RaceDataSync__WeatherDelayBetweenRequestsMilliseconds = $DelayMilliseconds.ToString($culture)
$connectionString = [Environment]::GetEnvironmentVariable(
    'ConnectionStrings__HorseRacing',
    'User')
if ([string]::IsNullOrWhiteSpace($connectionString)) {
    $connectionString =
        'Host=localhost;Port=5433;Database=horse_racing;' +
        'Username=horse_racing;Password=horse_racing_local'
}
$env:ConnectionStrings__HorseRacing = $connectionString

New-Item -ItemType Directory -Path $stateDirectory -Force | Out-Null
$completed = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::Ordinal)
if (Test-Path -LiteralPath $progressPath) {
    foreach ($row in Import-Csv -LiteralPath $progressPath) {
        if ($row.Status -eq 'Succeeded') {
            [void]$completed.Add($row.Month)
        }
    }
}

$month = [datetime]::new($From.Year, $From.Month, 1)
$lastMonth = [datetime]::new($To.Year, $To.Month, 1)
while ($month -le $lastMonth) {
    $monthKey = $month.ToString('yyyy-MM', $culture)
    $monthFrom = if ($month -lt $From.Date) { $From.Date } else { $month }
    $monthEnd = $month.AddMonths(1).AddDays(-1)
    $monthTo = if ($monthEnd -gt $To.Date) { $To.Date } else { $monthEnd }

    if ($completed.Contains($monthKey)) {
        Write-Output "Weather $monthKey already completed; skipping."
        $month = $month.AddMonths(1)
        continue
    }

    $succeeded = $false
    for ($attempt = 1; $attempt -le $MaximumAttempts; $attempt++) {
        Write-Output "Weather $monthKey attempt $attempt of $MaximumAttempts."
        & dotnet run --no-build --project $project --configuration Release -- `
            --mode weather `
            --from $monthFrom.ToString('yyyy-MM-dd', $culture) `
            --to $monthTo.ToString('yyyy-MM-dd', $culture)
        if ($LASTEXITCODE -eq 0) {
            $succeeded = $true
            break
        }

        if ($attempt -lt $MaximumAttempts) {
            $waitSeconds = 30 * $attempt
            Write-Warning "Weather $monthKey failed; retrying in $waitSeconds seconds."
            Start-Sleep -Seconds $waitSeconds
        }
    }

    [pscustomobject]@{
        Month = $monthKey
        From = $monthFrom.ToString('yyyy-MM-dd', $culture)
        To = $monthTo.ToString('yyyy-MM-dd', $culture)
        Status = if ($succeeded) { 'Succeeded' } else { 'Failed' }
        CompletedAtUtc = [datetime]::UtcNow.ToString('O', $culture)
    } | Export-Csv -LiteralPath $progressPath -NoTypeInformation -Append

    if (-not $succeeded) {
        throw "Weather enrichment failed for $monthKey after $MaximumAttempts attempts."
    }

    $month = $month.AddMonths(1)
}

Write-Output 'Kaggle Raceform weather enrichment completed.'
