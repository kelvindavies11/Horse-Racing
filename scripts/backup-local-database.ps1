[CmdletBinding()]
param(
    [string] $BackupDirectory,
    [ValidateRange(1, 365)]
    [int] $DailyRetention = 14,
    [ValidateRange(1, 104)]
    [int] $WeeklyRetention = 8,
    [ValidateRange(1, 120)]
    [int] $MonthlyRetention = 12
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-ConfiguredConnectionString {
    foreach ($scope in @('Process', 'User', 'Machine')) {
        $value = [Environment]::GetEnvironmentVariable('ConnectionStrings__HorseRacing', $scope)
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            return $value
        }
    }

    throw 'ConnectionStrings__HorseRacing is not configured for this process or Windows user.'
}

function Get-ConnectionValue(
    [System.Data.Common.DbConnectionStringBuilder] $Builder,
    [string[]] $Names,
    [string] $DefaultValue = $null
) {
    foreach ($name in $Names) {
        if ($Builder.ContainsKey($name)) {
            return [string] $Builder[$name]
        }
    }

    return $DefaultValue
}

function Find-PostgresTool([string] $Name) {
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    $postgresRoot = 'C:\Program Files\PostgreSQL'
    if (Test-Path -LiteralPath $postgresRoot) {
        $versions = Get-ChildItem -LiteralPath $postgresRoot -Directory | Sort-Object {
            $parsed = [version]'0.0'
            if ([version]::TryParse($_.Name, [ref] $parsed)) { $parsed } else { [version]'0.0' }
        } -Descending

        foreach ($version in $versions) {
            $candidate = Join-Path $version.FullName "bin\$Name.exe"
            if (Test-Path -LiteralPath $candidate) {
                return $candidate
            }
        }
    }

    throw "$Name was not found. Install PostgreSQL client tools or add them to PATH."
}

function Resolve-BackupDirectory([string] $RequestedDirectory) {
    if ([string]::IsNullOrWhiteSpace($RequestedDirectory)) {
        $oneDriveRoot = if (-not [string]::IsNullOrWhiteSpace($env:OneDriveConsumer)) {
            $env:OneDriveConsumer
        } else {
            $env:OneDrive
        }

        if ([string]::IsNullOrWhiteSpace($oneDriveRoot)) {
            throw 'No backup directory was supplied and the OneDrive folder could not be found.'
        }

        $RequestedDirectory = Join-Path $oneDriveRoot 'Backups\Horse-Racing'
    }

    $resolved = [System.IO.Path]::GetFullPath($RequestedDirectory)
    $repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
    $repositoryPrefix = $repositoryRoot.TrimEnd('\') + '\'
    if ($resolved.Equals($repositoryRoot, [StringComparison]::OrdinalIgnoreCase) -or
        $resolved.StartsWith($repositoryPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Database backups must be stored outside the Git repository.'
    }

    New-Item -ItemType Directory -Path $resolved -Force | Out-Null
    return $resolved
}

function Get-IsoWeekKey([datetime] $Timestamp) {
    $dayNumber = [int] $Timestamp.DayOfWeek
    if ($dayNumber -eq 0) { $dayNumber = 7 }
    $weekYear = $Timestamp.AddDays(4 - $dayNumber).Year
    $week = [Globalization.CultureInfo]::InvariantCulture.Calendar.GetWeekOfYear(
        $Timestamp,
        [Globalization.CalendarWeekRule]::FirstFourDayWeek,
        [DayOfWeek]::Monday)
    return '{0:D4}-W{1:D2}' -f $weekYear, $week
}

function Remove-ExpiredBackups(
    [string] $Directory,
    [int] $DailyCount,
    [int] $WeeklyCount,
    [int] $MonthlyCount
) {
    $backupPattern = '^horse-racing-(\d{8})-(\d{6})\.dump$'
    $backups = @(Get-ChildItem -LiteralPath $Directory -File -Filter 'horse-racing-*.dump' |
        ForEach-Object {
            if ($_.Name -match $backupPattern) {
                [pscustomobject]@{
                    File = $_
                    Timestamp = [datetime]::ParseExact(
                        "$($Matches[1])$($Matches[2])",
                        'yyyyMMddHHmmss',
                        [Globalization.CultureInfo]::InvariantCulture)
                }
            }
        } |
        Sort-Object Timestamp -Descending)

    $keep = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $backups | Select-Object -First $DailyCount | ForEach-Object {
        [void] $keep.Add($_.File.FullName)
    }

    $backups |
        Group-Object { Get-IsoWeekKey $_.Timestamp } |
        ForEach-Object { $_.Group | Sort-Object Timestamp -Descending | Select-Object -First 1 } |
        Sort-Object Timestamp -Descending |
        Select-Object -First $WeeklyCount |
        ForEach-Object { [void] $keep.Add($_.File.FullName) }

    $backups |
        Group-Object { $_.Timestamp.ToString('yyyy-MM', [Globalization.CultureInfo]::InvariantCulture) } |
        ForEach-Object { $_.Group | Sort-Object Timestamp -Descending | Select-Object -First 1 } |
        Sort-Object Timestamp -Descending |
        Select-Object -First $MonthlyCount |
        ForEach-Object { [void] $keep.Add($_.File.FullName) }

    foreach ($backup in $backups) {
        if ($keep.Contains($backup.File.FullName)) {
            continue
        }

        foreach ($path in @(
            $backup.File.FullName,
            "$($backup.File.FullName).sha256",
            "$($backup.File.FullName).json"
        )) {
            if (Test-Path -LiteralPath $path) {
                Remove-Item -LiteralPath $path -Force
            }
        }
    }

    Get-ChildItem -LiteralPath $Directory -File -Filter 'horse-racing-*.dump.partial' |
        Where-Object LastWriteTimeUtc -lt ([datetime]::UtcNow.AddDays(-1)) |
        Remove-Item -Force
}

$mutex = [Threading.Mutex]::new($false, 'Local\HorseRacingDatabaseBackup')
$lockAcquired = $false
$previousPgPassword = $env:PGPASSWORD
$hadPgPassword = Test-Path Env:PGPASSWORD
$temporaryPath = $null

try {
    try {
        $lockAcquired = $mutex.WaitOne(0)
    } catch [Threading.AbandonedMutexException] {
        $lockAcquired = $true
    }

    if (-not $lockAcquired) {
        Write-Output 'A database backup is already running; this invocation was skipped.'
        exit 0
    }

    $resolvedBackupDirectory = Resolve-BackupDirectory $BackupDirectory
    $connectionBuilder = [System.Data.Common.DbConnectionStringBuilder]::new()
    $connectionBuilder.set_ConnectionString((Get-ConfiguredConnectionString))

    $hostName = Get-ConnectionValue $connectionBuilder @('Host', 'Server') '127.0.0.1'
    $port = Get-ConnectionValue $connectionBuilder @('Port') '5432'
    $database = Get-ConnectionValue $connectionBuilder @('Database', 'Initial Catalog')
    $username = Get-ConnectionValue $connectionBuilder @('Username', 'User ID', 'UserId')
    $password = Get-ConnectionValue $connectionBuilder @('Password')

    if ([string]::IsNullOrWhiteSpace($database) -or [string]::IsNullOrWhiteSpace($username)) {
        throw 'The PostgreSQL connection string must include Database and Username.'
    }

    $pgDump = Find-PostgresTool 'pg_dump'
    $pgRestore = Find-PostgresTool 'pg_restore'
    $env:PGPASSWORD = $password

    $timestamp = Get-Date
    $baseName = 'horse-racing-{0}.dump' -f $timestamp.ToString('yyyyMMdd-HHmmss')
    $finalPath = Join-Path $resolvedBackupDirectory $baseName
    $temporaryPath = "$finalPath.partial"

    & $pgDump @(
        '--host', $hostName,
        '--port', $port,
        '--username', $username,
        '--dbname', $database,
        '--format=custom',
        '--compress=6',
        '--no-owner',
        '--no-privileges',
        '--file', $temporaryPath
    )
    if ($LASTEXITCODE -ne 0) {
        throw "pg_dump failed with exit code $LASTEXITCODE."
    }

    & $pgRestore --list $temporaryPath *> $null
    if ($LASTEXITCODE -ne 0) {
        throw "pg_restore could not read the new archive (exit code $LASTEXITCODE)."
    }

    Move-Item -LiteralPath $temporaryPath -Destination $finalPath
    $temporaryPath = $null

    $backupFile = Get-Item -LiteralPath $finalPath
    $hash = (Get-FileHash -LiteralPath $finalPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -LiteralPath "$finalPath.sha256" -Value $hash -Encoding utf8

    $manifest = [ordered]@{
        formatVersion = 1
        database = $database
        createdAtUtc = [datetime]::UtcNow.ToString('o')
        archiveFile = $backupFile.Name
        sizeBytes = $backupFile.Length
        sha256 = $hash
        validation = 'pg_restore --list'
        retention = [ordered]@{
            daily = $DailyRetention
            weekly = $WeeklyRetention
            monthly = $MonthlyRetention
        }
    }
    $manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath "$finalPath.json" -Encoding utf8

    Remove-ExpiredBackups $resolvedBackupDirectory $DailyRetention $WeeklyRetention $MonthlyRetention

    Write-Output ("Backup complete: {0} ({1:N1} MB)" -f $finalPath, ($backupFile.Length / 1MB))
} finally {
    if ($null -ne $temporaryPath -and (Test-Path -LiteralPath $temporaryPath)) {
        Remove-Item -LiteralPath $temporaryPath -Force
    }

    if ($hadPgPassword) {
        $env:PGPASSWORD = $previousPgPassword
    } else {
        Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    }

    if ($lockAcquired) {
        $mutex.ReleaseMutex()
    }
    $mutex.Dispose()
}
