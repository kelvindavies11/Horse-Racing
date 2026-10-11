[CmdletBinding()]
param(
    [datetime] $DailyTime = [datetime]::Today.AddHours(6)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runnerPath = Join-Path $repositoryRoot 'scripts\run-rolling-race-window.ps1'
$powershellPath = Join-Path $PSHOME 'powershell.exe'
if (-not (Test-Path -LiteralPath $powershellPath)) {
    $powershellPath = (Get-Command powershell.exe -ErrorAction Stop).Source
}

$arguments = @(
    '-NoProfile'
    '-NonInteractive'
    '-ExecutionPolicy Bypass'
    ('-File "{0}"' -f $runnerPath)
) -join ' '

$action = New-ScheduledTaskAction `
    -Execute $powershellPath `
    -Argument $arguments `
    -WorkingDirectory $repositoryRoot
$trigger = New-ScheduledTaskTrigger -Daily -At $DailyTime
$settings = New-ScheduledTaskSettingsSet `
    -StartWhenAvailable `
    -DontStopIfGoingOnBatteries `
    -AllowStartIfOnBatteries `
    -MultipleInstances IgnoreNew
$principal = New-ScheduledTaskPrincipal `
    -UserId ([System.Security.Principal.WindowsIdentity]::GetCurrent().Name) `
    -LogonType Interactive `
    -RunLevel Limited
$task = New-ScheduledTask `
    -Action $action `
    -Trigger $trigger `
    -Settings $settings `
    -Principal $principal `
    -Description 'Refreshes horse-racing data for seven days before today, today, and one month ahead.'

Register-ScheduledTask `
    -TaskName 'Horse Racing - Daily Race Window' `
    -InputObject $task `
    -Force `
    -ErrorAction Stop | Out-Null

Write-Output (
    'Registered Horse Racing - Daily Race Window for {0} each day.' -f
    $DailyTime.ToString('HH:mm'))
