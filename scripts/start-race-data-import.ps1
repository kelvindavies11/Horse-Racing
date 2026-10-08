[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('year-2026')]
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
}

$activeImports = @(Get-CimInstance Win32_Process | Where-Object {
    $_.ProcessId -ne $PID -and
    -not [string]::IsNullOrWhiteSpace($_.CommandLine) -and
    ($_.CommandLine -match 'HorseRacing\.RaceDataSync' -or
        $_.CommandLine -match 'HorseRacing\.Bha\.CuratedPromoter' -or
        $_.CommandLine -match 'resume-after-throttle\.ps1' -or
        $_.CommandLine -match 'resume-monthly-race-data-history\.ps1' -or
        $_.CommandLine -match 'resume-available-race-data-tail\.ps1')
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
