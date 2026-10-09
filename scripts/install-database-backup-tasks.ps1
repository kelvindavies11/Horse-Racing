[CmdletBinding()]
param(
    [string] $BackupDirectory,
    [ValidatePattern('^([01]\d|2[0-3]):[0-5]\d$')]
    [string] $DailyBackupTime = '02:00',
    [ValidatePattern('^([01]\d|2[0-3]):[0-5]\d$')]
    [string] $MonthlyRestoreCheckTime = '03:30'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$userConnection = [Environment]::GetEnvironmentVariable('ConnectionStrings__HorseRacing', 'User')
if ([string]::IsNullOrWhiteSpace($userConnection)) {
    throw 'ConnectionStrings__HorseRacing must be configured as a Windows user environment variable before installing the tasks.'
}

if ([string]::IsNullOrWhiteSpace($BackupDirectory)) {
    $oneDriveRoot = if (-not [string]::IsNullOrWhiteSpace($env:OneDriveConsumer)) {
        $env:OneDriveConsumer
    } else { $env:OneDrive }
    if ([string]::IsNullOrWhiteSpace($oneDriveRoot)) {
        throw 'No backup directory was supplied and the OneDrive folder could not be found.'
    }
    $BackupDirectory = Join-Path $oneDriveRoot 'Backups\Horse-Racing'
}

$BackupDirectory = [System.IO.Path]::GetFullPath($BackupDirectory)
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ($BackupDirectory.Equals($repositoryRoot, [StringComparison]::OrdinalIgnoreCase) -or
    $BackupDirectory.StartsWith($repositoryRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Database backups must be stored outside the Git repository.'
}
New-Item -ItemType Directory -Path $BackupDirectory -Force | Out-Null

$pwshPath = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
if (-not (Test-Path -LiteralPath $pwshPath)) {
    throw "Windows PowerShell was not found at $pwshPath."
}
$backupScript = Join-Path $PSScriptRoot 'backup-local-database.ps1'
$restoreScript = Join-Path $PSScriptRoot 'test-database-backup.ps1'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent().Name

function Quote-TaskArgument([string] $Value) {
    return '"' + $Value.Replace('"', '\"') + '"'
}

$backupArguments = @(
    '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass',
    '-File', (Quote-TaskArgument $backupScript),
    '-BackupDirectory', (Quote-TaskArgument $BackupDirectory)
) -join ' '
$restoreArguments = @(
    '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass',
    '-File', (Quote-TaskArgument $restoreScript),
    '-BackupDirectory', (Quote-TaskArgument $BackupDirectory),
    '-RunOnlyOnDayOfMonth', '1'
) -join ' '

$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -StartWhenAvailable `
    -MultipleInstances IgnoreNew `
    -ExecutionTimeLimit (New-TimeSpan -Hours 6)
$principal = New-ScheduledTaskPrincipal -UserId $identity -LogonType Interactive -RunLevel Limited

$backupTask = New-ScheduledTask `
    -Action (New-ScheduledTaskAction -Execute $pwshPath -Argument $backupArguments) `
    -Trigger (New-ScheduledTaskTrigger -Daily -At $DailyBackupTime) `
    -Settings $settings `
    -Principal $principal `
    -Description 'Creates and validates a compressed backup of the local Horse Racing PostgreSQL database.'
Register-ScheduledTask -TaskName 'Horse Racing - Nightly Database Backup' -InputObject $backupTask -Force -ErrorAction Stop | Out-Null

$restoreTask = New-ScheduledTask `
    -Action (New-ScheduledTaskAction -Execute $pwshPath -Argument $restoreArguments) `
    -Trigger (New-ScheduledTaskTrigger -Daily -At $MonthlyRestoreCheckTime) `
    -Settings $settings `
    -Principal $principal `
    -Description 'On the first day of each month, restores the latest Horse Racing backup into a temporary database and verifies it.'
Register-ScheduledTask -TaskName 'Horse Racing - Monthly Database Restore Check' -InputObject $restoreTask -Force -ErrorAction Stop | Out-Null

Write-Output "Nightly backup scheduled for $DailyBackupTime."
Write-Output "Monthly restore check scheduled for day 1 at $MonthlyRestoreCheckTime."
Write-Output "Backup directory: $BackupDirectory"
