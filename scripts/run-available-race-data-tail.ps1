[CmdletBinding()]
param(
    [ValidatePattern('^\d{4}-\d{2}$')]
    [string] $StartMonth = '2026-01',

    [ValidatePattern('^\d{4}-\d{2}$')]
    [string] $EndMonth = '2026-12',

    [string] $StateDirectory = 'artifacts/2026-results-sync',

    [ValidatePattern('^\d{4}-\d{2}-\d{2}$')]
    [string] $ThroughDate,

    [switch] $DryRun,

    [switch] $SkipBuild,

    [ValidateRange(1, 5)]
    [int] $MaxParallelism = 2
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$rawProjectPath = Join-Path $repositoryRoot 'src\HorseRacing.RaceDataSync'
$curatedProjectPath = Join-Path $repositoryRoot 'src\HorseRacing.Bha.CuratedPromoter'
$solutionPath = Join-Path $repositoryRoot 'HorseRacing.sln'
. (Join-Path $PSScriptRoot 'parallel-import-runner.ps1')
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
$attemptedRanges = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::Ordinal)
if (Test-Path -LiteralPath $progressPath) {
    foreach ($entry in Import-Csv -LiteralPath $progressPath) {
        [void] $attemptedRanges.Add("$($entry.From)|$($entry.To)")
        if ($entry.Status -ne 'Succeeded') {
            continue
        }

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
            'or user environment before starting the available-results sync.')
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
    $env:BhaCuratedPromotion__BatchSize = '1000'

    if (-not $SkipBuild) {
        & dotnet build $solutionPath --no-restore
        if ($LASTEXITCODE -ne 0) {
            throw "The solution build failed with exit code $LASTEXITCODE."
        }
    }
}

$failedRanges = [System.Collections.Generic.List[string]]::new()
$pendingMonths = [System.Collections.Generic.List[object]]::new()
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
        '[{0}/{1}] {2} through {3}: restartable Raw collection with periodic Curated checkpoints{4}' -f
        $month.Index,
        $months.Count,
        $month.From,
        $month.To,
        $(if ($month.IsFinal) { '' } else { ' (partial month)' }))

    if ($DryRun) {
        continue
    }

    $isSameRangeRetry = $attemptedRanges.Contains("$($month.From)|$($month.To)")
    $reuseSuccessful = $month.IsFinal -or $isSameRangeRetry
    $month | Add-Member -NotePropertyName ReuseSuccessful -NotePropertyValue $reuseSuccessful
    $pendingMonths.Add($month)
}

if ($DryRun) {
    Write-Output 'Dry run complete; no network requests or database writes were made.'
    return
}

for ($batchStart = 0; $batchStart -lt $pendingMonths.Count; $batchStart += $MaxParallelism) {
    $batchEnd = [Math]::Min($batchStart + $MaxParallelism - 1, $pendingMonths.Count - 1)
    $batch = @($pendingMonths[$batchStart..$batchEnd])
    Write-Output (
        'Launching Raw batch {0}-{1} with {2} protected runners.' -f
        ($batchStart + 1),
        ($batchEnd + 1),
        $batch.Count)
    $rawResults = @(Invoke-ParallelRawBatch `
        -Months $batch `
        -RawProjectPath $rawProjectPath `
        -CuratedProjectPath $curatedProjectPath `
        -StatePath $statePath)

    foreach ($rawResult in $rawResults) {
        $month = $rawResult.Month
        $exitCode = if ($rawResult.RawExitCode -eq 0 -and $rawResult.CuratedExitCode -eq 0) { 0 } else { 1 }
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
            Write-Warning (
                'The available range {0} through {1} needs retrying (Raw exit {2}, Curated exit {3}).' -f
                $month.From,
                $month.To,
                $rawResult.RawExitCode,
                $rawResult.CuratedExitCode)
        }
    }

    if ($failedRanges.Count -gt 0) {
        break
    }
}

if ($failedRanges.Count -gt 0) {
    Write-Error ('The following ranges need retrying: {0}' -f ($failedRanges -join ', '))
    exit 1
}

Write-Output "All result dates currently available through $($availableThrough.ToString('yyyy-MM-dd', $culture)) are covered."
