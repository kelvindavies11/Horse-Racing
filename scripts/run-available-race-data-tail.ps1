[CmdletBinding()]
param(
    [ValidatePattern('^\d{4}-\d{2}$')]
    [string] $StartMonth = '2026-10',

    [ValidatePattern('^\d{4}-\d{2}$')]
    [string] $EndMonth = '2026-12',

    [string] $StateDirectory = 'artifacts/2026-q4-sync',

    [ValidatePattern('^\d{4}-\d{2}-\d{2}$')]
    [string] $ThroughDate,

    [switch] $DryRun,

    [switch] $SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectPath = Join-Path $repositoryRoot 'src\HorseRacing.RaceDataSync'
$solutionPath = Join-Path $repositoryRoot 'HorseRacing.sln'
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

$londonTimeZone = [TimeZoneInfo]::FindSystemTimeZoneById('Europe/London')
$today = if ([string]::IsNullOrWhiteSpace($ThroughDate)) {
    [TimeZoneInfo]::ConvertTime([DateTimeOffset]::UtcNow, $londonTimeZone).Date
}
else {
    [datetime]::ParseExact($ThroughDate, 'yyyy-MM-dd', $culture)
}
$finalDate = $lastMonth.AddMonths(1).AddDays(-1)
$availableThrough = if ($today -lt $finalDate) { $today } else { $finalDate }

if ($availableThrough -lt $firstMonth) {
    Write-Output "No dates in $StartMonth through $EndMonth are available yet."
    return
}

$months = [System.Collections.Generic.List[object]]::new()
$cursor = $firstMonth
while ($cursor -le $lastMonth -and $cursor -le $availableThrough) {
    $monthEnd = $cursor.AddMonths(1).AddDays(-1)
    $targetEnd = if ($availableThrough -lt $monthEnd) { $availableThrough } else { $monthEnd }
    $months.Add([pscustomobject]@{
        Index = $months.Count + 1
        From = $cursor.ToString('yyyy-MM-dd', $culture)
        To = $targetEnd.ToString('yyyy-MM-dd', $culture)
        MonthEnd = $monthEnd.ToString('yyyy-MM-dd', $culture)
        IsFinal = $targetEnd -eq $monthEnd
    })
    $cursor = $cursor.AddMonths(1)
}

Write-Output (
    'Prepared {0} available monthly requests from {1} through {2}.' -f
    $months.Count,
    $months[0].From,
    $months[-1].To)

New-Item -ItemType Directory -Path $statePath -Force | Out-Null
$successfulCoverage = @{}
if (Test-Path -LiteralPath $progressPath) {
    foreach ($entry in Import-Csv -LiteralPath $progressPath | Where-Object Status -eq 'Succeeded') {
        $entryTo = [datetime]::ParseExact($entry.To, 'yyyy-MM-dd', $culture)
        $coveredTo = if ($successfulCoverage.ContainsKey($entry.From)) {
            [datetime]::ParseExact($successfulCoverage[$entry.From], 'yyyy-MM-dd', $culture)
        }
        else {
            [datetime]::MinValue
        }
        if ($entryTo -gt $coveredTo) {
            $successfulCoverage[$entry.From] = $entry.To
        }
    }
}

if (-not $DryRun) {
    $token = [Environment]::GetEnvironmentVariable(
        'BhaCollection__RacingStatusApiBearerToken',
        'Process')
    if ([string]::IsNullOrWhiteSpace($token)) {
        $token = [Environment]::GetEnvironmentVariable(
            'BhaCollection__RacingStatusApiBearerToken',
            'User')
    }
    if ([string]::IsNullOrWhiteSpace($token)) {
        throw (
            'Set BhaCollection__RacingStatusApiBearerToken in the current process ' +
            'or user environment before starting the year-end sync.')
    }
    $env:BhaCollection__RacingStatusApiBearerToken = $token

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

    if (-not $SkipBuild) {
        & dotnet build $solutionPath --no-restore
        if ($LASTEXITCODE -ne 0) {
            throw "The solution build failed with exit code $LASTEXITCODE."
        }
    }
}

$failedRanges = [System.Collections.Generic.List[string]]::new()
foreach ($month in $months) {
    $coveredTo = $successfulCoverage[$month.From]
    $targetTo = [datetime]::ParseExact($month.To, 'yyyy-MM-dd', $culture)
    if ($coveredTo -and [datetime]::ParseExact($coveredTo, 'yyyy-MM-dd', $culture) -ge $targetTo) {
        Write-Output (
            '[{0}/{1}] {2} through {3}: already covered; skipping' -f
            $month.Index,
            $months.Count,
            $month.From,
            $month.To)
        continue
    }

    Write-Output (
        '[{0}/{1}] {2} through {3}: Raw collection, Curated promotion, weather enrichment{4}' -f
        $month.Index,
        $months.Count,
        $month.From,
        $month.To,
        $(if ($month.IsFinal) { '' } else { ' (partial month)' }))

    if ($DryRun) {
        continue
    }

    & dotnet run --no-build --project $projectPath -- `
        --from $month.From `
        --to $month.To

    $exitCode = $LASTEXITCODE
    $status = if ($exitCode -eq 0) { 'Succeeded' } else { 'Failed' }
    [pscustomobject]@{
        MonthIndex = $month.Index
        From = $month.From
        To = $month.To
        TargetMonthEnd = $month.MonthEnd
        IsFinal = $month.IsFinal
        Status = $status
        ExitCode = $exitCode
        CompletedAtUtc = [datetime]::UtcNow.ToString('O', $culture)
    } | Export-Csv -LiteralPath $progressPath -NoTypeInformation -Append

    if ($exitCode -ne 0) {
        $failedRanges.Add("$($month.From):$($month.To)")
        Write-Warning "The available range $($month.From) through $($month.To) needs retrying."
    }
}

if ($DryRun) {
    Write-Output 'Dry run complete; no network requests or database writes were made.'
    return
}

if ($failedRanges.Count -gt 0) {
    Write-Error ('The following ranges need retrying: {0}' -f ($failedRanges -join ', '))
    exit 1
}

Write-Output "All result dates currently available through $($availableThrough.ToString('yyyy-MM-dd', $culture)) are covered."
