[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('daily-window')]
    [string] $PhaseId
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$pwshPath = (Get-Command pwsh -ErrorAction Stop).Source

$phases = @{
    'daily-window' = @{
        Script = 'run-rolling-race-window.ps1'
        StateDirectory = 'artifacts/daily-race-window'
    }
}

$activeImports = @(Get-CimInstance Win32_Process | Where-Object {
    $isWorker = $_.Name -match '^dotnet(?:\.exe)?$' -and
        ($_.CommandLine -match 'HorseRacing\.RaceDataSync' -or
            $_.CommandLine -match 'HorseRacing\.Bha\.CuratedPromoter')
    $isSupervisor = $_.Name -match '^pwsh(?:\.exe)?$' -and
        $_.CommandLine -match '-File\s+.*(?:resume-after-throttle|resume-monthly-race-data-history|resume-available-race-data-tail|run-rolling-race-window)\.ps1(?:"|\s|$)'
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
$stdoutPath = Join-Path $statePath 'run.stdout.log'
$stderrPath = Join-Path $statePath 'run.stderr.log'

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
    '-RequestDelayMilliseconds',
    '9000'
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
