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

    [ValidateRange(1, 240)]
    [int] $StartIndex = 1,

    [switch] $DryRun,

    [switch] $SkipBuild,

    [switch] $Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$rawProjectPath = Join-Path $repositoryRoot 'src\HorseRacing.RaceDataSync'
$curatedProjectPath = Join-Path $repositoryRoot 'src\HorseRacing.Bha.CuratedPromoter'
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

$monthCount = (($lastMonth.Year - $firstMonth.Year) * 12) +
    $lastMonth.Month - $firstMonth.Month + 1
if ($monthCount -gt 240) {
    throw 'The monthly batch is limited to 240 months.'
}
if ($StartIndex -gt $monthCount) {
    throw 'StartIndex cannot exceed the number of requested months.'
}

$months = for ($offset = 0; $offset -lt $monthCount; $offset++) {
    $from = $firstMonth.AddMonths($offset)
    [pscustomobject]@{
        Index = $offset + 1
        From = $from.ToString('yyyy-MM-dd', $culture)
        To = $from.AddMonths(1).AddDays(-1).ToString('yyyy-MM-dd', $culture)
    }
}

Write-Output (
    'Prepared {0} monthly requests from {1} through {2}.' -f
    $monthCount,
    $months[0].From,
    $months[-1].To)

if (-not $DryRun) {
    New-Item -ItemType Directory -Path $statePath -Force | Out-Null

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
            'or user environment before starting the historical sync.')
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

$failedMonths = [System.Collections.Generic.List[string]]::new()
$completedMonths = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::Ordinal)

if ((Test-Path -LiteralPath $progressPath) -and -not $Force) {
    foreach ($entry in Import-Csv -LiteralPath $progressPath) {
        if ($entry.Status -eq 'Succeeded') {
            [void] $completedMonths.Add($entry.From)
        }
    }
}

foreach ($month in $months | Where-Object Index -ge $StartIndex) {
    if ($completedMonths.Contains($month.From)) {
        Write-Output (
            '[{0}/{1}] {2} through {3}: already completed; skipping' -f
            $month.Index,
            $monthCount,
            $month.From,
            $month.To)
        continue
    }

    Write-Output (
        '[{0}/{1}] {2} through {3}: restartable Raw collection, then Curated drain' -f
        $month.Index,
        $monthCount,
        $month.From,
        $month.To)

    if ($DryRun) {
        continue
    }

    Write-Output '  Raw job: collecting only missing payloads.'
    & dotnet run --no-build --project $rawProjectPath -- `
        --mode raw `
        --reuse-successful `
        --from $month.From `
        --to $month.To

    $rawExitCode = $LASTEXITCODE

    Write-Output '  Curated job: draining every pending Raw payload.'
    & dotnet run --no-build --project $curatedProjectPath -- --drain
    $curatedExitCode = $LASTEXITCODE

    $exitCode = if ($rawExitCode -eq 0 -and $curatedExitCode -eq 0) { 0 } else { 1 }
    $status = if ($exitCode -eq 0) { 'Succeeded' } else { 'Failed' }
    [pscustomobject]@{
        MonthIndex = $month.Index
        From = $month.From
        To = $month.To
        Status = $status
        ExitCode = $exitCode
        CompletedAtUtc = [datetime]::UtcNow.ToString('O', $culture)
    } | Export-Csv -LiteralPath $progressPath -NoTypeInformation -Append

    if ($exitCode -ne 0) {
        $failedMonths.Add("$($month.Index):$($month.From):$($month.To)")
        Write-Warning (
            'Month {0} needs retrying (Raw exit {1}, Curated exit {2}); stopping this pass so the supervisor can wait and resume.' -f
            $month.Index,
            $rawExitCode,
            $curatedExitCode)
        break
    }
}

if ($DryRun) {
    Write-Output 'Dry run complete; no network requests or database writes were made.'
    return
}

if ($failedMonths.Count -gt 0) {
    Write-Error (
        'The following monthly runs need retrying: {0}' -f
        ($failedMonths -join ', '))
    exit 1
}

Write-Output "All $monthCount restartable Raw jobs and following Curated drains completed successfully."
