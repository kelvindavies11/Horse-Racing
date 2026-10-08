[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('year-2026', 'year-2024', 'year-2023', 'year-2022', 'year-2021', 'year-2020')]
    [string] $PhaseId
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$pwshPath = (Get-Command pwsh -ErrorAction Stop).Source

$phases = @{
    'year-2026' = @{
        Script = 'resume-available-race-data-tail.ps1'
        StartMonth = '2026-01'
        EndMonth = '2026-12'
        StateDirectory = 'artifacts/2026-results-sync'
    }
    'year-2024' = @{
        Script = 'resume-monthly-race-data-history.ps1'
        StartMonth = '2024-01'
        EndMonth = '2024-12'
        StateDirectory = 'artifacts/2024-results-sync'
    }
    'year-2023' = @{
        Script = 'resume-monthly-race-data-history.ps1'
        StartMonth = '2023-01'
        EndMonth = '2023-12'
        StateDirectory = 'artifacts/2023-results-sync'
    }
    'year-2022' = @{
        Script = 'resume-monthly-race-data-history.ps1'
        StartMonth = '2022-01'
        EndMonth = '2022-12'
        StateDirectory = 'artifacts/2022-results-sync'
    }
    'year-2021' = @{
        Script = 'resume-monthly-race-data-history.ps1'
        StartMonth = '2021-01'
        EndMonth = '2021-12'
        StateDirectory = 'artifacts/2021-results-sync'
    }
    'year-2020' = @{
        Script = 'resume-monthly-race-data-history.ps1'
        StartMonth = '2020-01'
        EndMonth = '2020-12'
        StateDirectory = 'artifacts/2020-results-sync'
    }
}

$activeImports = @(Get-CimInstance Win32_Process | Where-Object {
    $isWorker = $_.Name -match '^dotnet(?:\.exe)?$' -and
        ($_.CommandLine -match 'HorseRacing\.RaceDataSync' -or
            $_.CommandLine -match 'HorseRacing\.Bha\.CuratedPromoter')
    $isSupervisor = $_.Name -match '^pwsh(?:\.exe)?$' -and
        $_.CommandLine -match '-File\s+.*(?:resume-after-throttle|resume-monthly-race-data-history|resume-available-race-data-tail)\.ps1(?:"|\s|$)'
    $_.ProcessId -ne $PID -and
    -not [string]::IsNullOrWhiteSpace($_.CommandLine) -and
    ($isWorker -or $isSupervisor)
})

if ($activeImports.Count -gt 0) {
    throw 'Another BHA import is already active. No new process was started.'
}

$phase = $phases[$PhaseId]
$scriptPath = Join-Path $PSScriptRoot $phase.Script
$statePath = Join-Path $repositoryRoot $phase.StateDirectory
$stdoutPath = Join-Path $statePath 'resume.stdout.log'
$stderrPath = Join-Path $statePath 'resume.stderr.log'

New-Item -ItemType Directory -Path $statePath -Force | Out-Null

function Quote-ProcessArgument([string] $Value) {
    return '"' + $Value.Replace('"', '\"') + '"'
}

$arguments = @(
    '-NoProfile',
    '-NonInteractive',
    '-ExecutionPolicy',
    'Bypass',
    '-File',
    (Quote-ProcessArgument $scriptPath),
    '-StartMonth',
    $phase.StartMonth,
    '-EndMonth',
    $phase.EndMonth,
    '-StateDirectory',
    (Quote-ProcessArgument $statePath),
    '-ProbeIntervalMinutes',
    '15',
    '-RequestDelayMilliseconds',
    '1000'
)

$process = Start-Process `
    -FilePath $pwshPath `
    -ArgumentList $arguments `
    -WorkingDirectory $repositoryRoot `
    -WindowStyle Hidden `
    -RedirectStandardOutput $stdoutPath `
    -RedirectStandardError $stderrPath `
    -PassThru

Write-Output $process.Id
