[CmdletBinding()]
param(
    [string] $BackupDirectory,
    [ValidateRange(0, 31)]
    [int] $RunOnlyOnDayOfMonth = 0
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($RunOnlyOnDayOfMonth -gt 0 -and (Get-Date).Day -ne $RunOnlyOnDayOfMonth) {
    Write-Output "Restore check is scheduled for day $RunOnlyOnDayOfMonth; nothing to do today."
    exit 0
}

function Find-PostgresTool([string] $Name) {
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($null -ne $command) { return $command.Source }

    $postgresRoot = 'C:\Program Files\PostgreSQL'
    if (Test-Path -LiteralPath $postgresRoot) {
        $versions = Get-ChildItem -LiteralPath $postgresRoot -Directory | Sort-Object {
            $parsed = [version]'0.0'
            if ([version]::TryParse($_.Name, [ref] $parsed)) { $parsed } else { [version]'0.0' }
        } -Descending
        foreach ($version in $versions) {
            $candidate = Join-Path $version.FullName "bin\$Name.exe"
            if (Test-Path -LiteralPath $candidate) { return $candidate }
        }
    }
    throw "$Name was not found. Install PostgreSQL client tools or add them to PATH."
}

function Resolve-BackupDirectory([string] $RequestedDirectory) {
    if ([string]::IsNullOrWhiteSpace($RequestedDirectory)) {
        $oneDriveRoot = if (-not [string]::IsNullOrWhiteSpace($env:OneDriveConsumer)) {
            $env:OneDriveConsumer
        } else { $env:OneDrive }
        if ([string]::IsNullOrWhiteSpace($oneDriveRoot)) {
            throw 'No backup directory was supplied and the OneDrive folder could not be found.'
        }
        $RequestedDirectory = Join-Path $oneDriveRoot 'Backups\Horse-Racing'
    }
    return [System.IO.Path]::GetFullPath($RequestedDirectory)
}

function Get-FreeTcpPort {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    try {
        $listener.Start()
        return ([Net.IPEndPoint] $listener.LocalEndpoint).Port
    } finally {
        $listener.Stop()
    }
}

$mutex = [Threading.Mutex]::new($false, 'Local\HorseRacingDatabaseRestoreCheck')
$lockAcquired = $false
$pgCtl = $null
$clusterStarted = $false
$workRoot = $null
$workDirectory = $null

try {
    try { $lockAcquired = $mutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $lockAcquired = $true }
    if (-not $lockAcquired) {
        Write-Output 'A database restore check is already running; this invocation was skipped.'
        exit 0
    }

    $resolvedBackupDirectory = Resolve-BackupDirectory $BackupDirectory
    $backup = Get-ChildItem -LiteralPath $resolvedBackupDirectory -File -Filter 'horse-racing-*.dump' |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    if ($null -eq $backup) {
        throw "No database backup was found in $resolvedBackupDirectory."
    }

    $checksumPath = "$($backup.FullName).sha256"
    if (-not (Test-Path -LiteralPath $checksumPath)) {
        throw "The backup checksum is missing: $checksumPath"
    }
    $expectedHash = (Get-Content -LiteralPath $checksumPath -Raw).Trim().ToLowerInvariant()
    $actualHash = (Get-FileHash -LiteralPath $backup.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $expectedHash) {
        throw "The backup checksum does not match: $($backup.FullName)"
    }

    $initdb = Find-PostgresTool 'initdb'
    $pgCtl = Find-PostgresTool 'pg_ctl'
    $createdb = Find-PostgresTool 'createdb'
    $pgRestore = Find-PostgresTool 'pg_restore'
    $psql = Find-PostgresTool 'psql'

    & $pgRestore --list $backup.FullName *> $null
    if ($LASTEXITCODE -ne 0) {
        throw "pg_restore could not read $($backup.FullName)."
    }

    $resultDirectory = Join-Path $resolvedBackupDirectory 'restore-checks'
    New-Item -ItemType Directory -Path $resultDirectory -Force | Out-Null
    $workRoot = [System.IO.Path]::GetFullPath((Join-Path $resultDirectory 'work'))
    New-Item -ItemType Directory -Path $workRoot -Force | Out-Null
    $workDirectory = [System.IO.Path]::GetFullPath((Join-Path $workRoot ('restore-{0}-{1}' -f (Get-Date).ToString('yyyyMMddHHmmss'), $PID)))
    if (-not $workDirectory.StartsWith($workRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The generated restore-check working directory was rejected.'
    }

    $dataDirectory = Join-Path $workDirectory 'data'
    $serverLog = Join-Path $workDirectory 'postgres.log'
    New-Item -ItemType Directory -Path $workDirectory -Force | Out-Null
    & $initdb @('--pgdata', $dataDirectory, '--username', 'postgres', '--auth=trust', '--encoding=UTF8', '--no-locale', '--no-sync') *> $null
    if ($LASTEXITCODE -ne 0) { throw "initdb failed with exit code $LASTEXITCODE." }

    $port = Get-FreeTcpPort
    $serverOptions = "-p $port -h 127.0.0.1 -c fsync=off -c synchronous_commit=off -c full_page_writes=off"
    & $pgCtl @('--pgdata', $dataDirectory, '--log', $serverLog, '--options', $serverOptions, '--wait', 'start')
    if ($LASTEXITCODE -ne 0) { throw "The isolated PostgreSQL restore-check server failed to start (exit code $LASTEXITCODE)." }
    $clusterStarted = $true

    $testDatabase = 'horse_racing_restore_check'
    & $createdb @('--host', '127.0.0.1', '--port', $port, '--username', 'postgres', $testDatabase)
    if ($LASTEXITCODE -ne 0) { throw "createdb failed with exit code $LASTEXITCODE." }

    & $pgRestore @(
        '--host', '127.0.0.1',
        '--port', $port,
        '--username', 'postgres',
        '--dbname', $testDatabase,
        '--exit-on-error',
        '--no-owner',
        '--no-privileges',
        '--jobs=2',
        $backup.FullName
    )
    if ($LASTEXITCODE -ne 0) { throw "pg_restore failed with exit code $LASTEXITCODE." }

    $verification = & $psql @(
        '--host', '127.0.0.1',
        '--port', $port,
        '--username', 'postgres',
        '--dbname', $testDatabase,
        '--tuples-only',
        '--no-align',
        '--field-separator', '|',
        '--command', "select (to_regclass('raw.collection_runs') is not null), (to_regclass('raw.payloads') is not null), (to_regclass('curated.promotion_runs') is not null), (to_regclass('curated.domain_objects') is not null), (select count(*) from raw.collection_runs), (select count(*) from curated.domain_objects);"
    )
    if ($LASTEXITCODE -ne 0) { throw "Restore verification query failed with exit code $LASTEXITCODE." }

    $fields = ([string] $verification).Trim() -split '\|'
    if ($fields.Count -ne 6 -or @($fields[0..3] | Where-Object { $_ -ne 't' }).Count -gt 0) {
        throw 'The restored database did not contain the expected Raw and Curated tables.'
    }

    $result = [ordered]@{
        checkedAtUtc = [datetime]::UtcNow.ToString('o')
        archiveFile = $backup.Name
        sha256 = $actualHash
        outcome = 'Succeeded'
        rawCollectionRuns = [long] $fields[4]
        curatedDomainObjects = [long] $fields[5]
    }
    $resultPath = Join-Path $resultDirectory ('restore-check-{0}.json' -f (Get-Date).ToString('yyyyMMdd-HHmmss'))
    $result | ConvertTo-Json | Set-Content -LiteralPath $resultPath -Encoding utf8
    Write-Output ("Restore check succeeded for {0}: {1} Raw runs and {2} Curated objects." -f $backup.Name, $fields[4], $fields[5])
} finally {
    if ($clusterStarted -and $null -ne $pgCtl) {
        & $pgCtl @('--pgdata', $dataDirectory, '--mode=fast', '--wait', 'stop')
    }

    if ($null -ne $workDirectory -and $null -ne $workRoot) {
        $resolvedWorkDirectory = [System.IO.Path]::GetFullPath($workDirectory)
        $resolvedWorkRoot = [System.IO.Path]::GetFullPath($workRoot)
        if ($resolvedWorkDirectory.StartsWith($resolvedWorkRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase) -and
            (Test-Path -LiteralPath $resolvedWorkDirectory)) {
            Remove-Item -LiteralPath $resolvedWorkDirectory -Recurse -Force
        }
    }

    if ($lockAcquired) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
}
