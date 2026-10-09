Set-StrictMode -Version Latest

function ConvertTo-ProcessArgument([string] $Value) {
    if ($Value -notmatch '[\s"]') {
        return $Value
    }

    return '"' + $Value.Replace('"', '\"') + '"'
}

function Invoke-SerializedCuratedDrain {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string] $CuratedProjectPath,

        [Parameter(Mandatory)]
        [string] $Reason
    )

    Write-Host ('  Starting serialized Curated drain ({0}).' -f $Reason)
    $curatedOutput = @(& dotnet run --no-build --project $CuratedProjectPath -- --drain 2>&1)
    $curatedExitCode = $LASTEXITCODE
    foreach ($line in $curatedOutput) {
        Write-Host ('  [curated] {0}' -f $line)
    }

    return $curatedExitCode
}

function Start-RaceWeatherWorker {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [object] $Month,

        [Parameter(Mandatory)]
        [string] $RawProjectPath,

        [Parameter(Mandatory)]
        [string] $StatePath
    )

    $monthKey = $Month.From.Substring(0, 7)
    $stdoutPath = Join-Path $StatePath "weather-$monthKey.stdout.log"
    $stderrPath = Join-Path $StatePath "weather-$monthKey.stderr.log"
    $arguments = @(
        'run',
        '--no-build',
        '--project',
        (ConvertTo-ProcessArgument $RawProjectPath),
        '--',
        '--mode',
        'weather',
        '--from',
        $Month.From,
        '--to',
        $Month.To)

    Write-Host ('  Starting Weather worker for {0} through {1}.' -f $Month.From, $Month.To)
    $process = Start-Process `
        -FilePath 'dotnet' `
        -ArgumentList $arguments `
        -WorkingDirectory ([System.IO.Path]::GetDirectoryName($RawProjectPath)) `
        -WindowStyle Hidden `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath `
        -PassThru

    return [pscustomobject]@{
        Month = $Month
        Process = $process
        StandardOutputPath = $stdoutPath
        StandardErrorPath = $stderrPath
    }
}

function Complete-RaceWeatherWorker {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [object] $Worker
    )

    $Worker.Process.WaitForExit()
    $exitCode = $Worker.Process.ExitCode
    if (Test-Path -LiteralPath $Worker.StandardOutputPath) {
        Get-Content -LiteralPath $Worker.StandardOutputPath | ForEach-Object {
            Write-Host ('  [weather {0}] {1}' -f $Worker.Month.From.Substring(0, 7), $_)
        }
    }
    if (Test-Path -LiteralPath $Worker.StandardErrorPath) {
        Get-Content -LiteralPath $Worker.StandardErrorPath | ForEach-Object {
            Write-Warning ('[weather {0}] {1}' -f $Worker.Month.From.Substring(0, 7), $_)
        }
    }

    Write-Host (
        '  [{0}] Weather worker exited {1}.' -f
        $Worker.Month.From.Substring(0, 7),
        $exitCode)
    $Worker.Process.Dispose()
    return $exitCode
}

function Update-RaceWeatherPipeline {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [object] $ActiveWorkers,

        [Parameter(Mandatory)]
        [object] $Backlog,

        [Parameter(Mandatory)]
        [string] $RawProjectPath,

        [Parameter(Mandatory)]
        [string] $StatePath,

        [Parameter(Mandatory)]
        [string] $ProgressPath,

        [switch] $WaitForCompletion
    )

    $failures = [System.Collections.Generic.List[object]]::new()
    $culture = [System.Globalization.CultureInfo]::InvariantCulture
    do {
        if ($ActiveWorkers.Count -eq 0 -and $Backlog.Count -gt 0) {
            $ActiveWorkers.Add((Start-RaceWeatherWorker `
                -Month $Backlog[0] `
                -RawProjectPath $RawProjectPath `
                -StatePath $StatePath))
            $Backlog.RemoveAt(0)
        }

        if ($WaitForCompletion -and $ActiveWorkers.Count -gt 0 -and
            -not $ActiveWorkers[0].Process.HasExited) {
            $ActiveWorkers[0].Process.WaitForExit()
        }

        foreach ($worker in $ActiveWorkers.ToArray()) {
            if (-not $worker.Process.HasExited) {
                continue
            }

            $exitCode = Complete-RaceWeatherWorker -Worker $worker
            $status = if ($exitCode -eq 0) { 'Succeeded' } else { 'Failed' }
            [pscustomobject]@{
                From = $worker.Month.From
                To = $worker.Month.To
                Status = $status
                ExitCode = $exitCode
                CompletedAtUtc = [datetime]::UtcNow.ToString('O', $culture)
            } | Export-Csv -LiteralPath $ProgressPath -NoTypeInformation -Append
            if ($exitCode -ne 0) {
                $failures.Add([pscustomobject]@{
                    Month = $worker.Month
                    ExitCode = $exitCode
                })
            }
            [void] $ActiveWorkers.Remove($worker)
        }

        if ($failures.Count -gt 0) {
            break
        }

        if (-not $WaitForCompletion) {
            if ($ActiveWorkers.Count -eq 0 -and $Backlog.Count -gt 0) {
                $ActiveWorkers.Add((Start-RaceWeatherWorker `
                    -Month $Backlog[0] `
                    -RawProjectPath $RawProjectPath `
                    -StatePath $StatePath))
                $Backlog.RemoveAt(0)
            }
            break
        }
    }
    while ($ActiveWorkers.Count -gt 0 -or $Backlog.Count -gt 0)

    return @($failures)
}

function Invoke-ParallelRawBatch {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [object[]] $Months,

        [Parameter(Mandatory)]
        [string] $RawProjectPath,

        [Parameter(Mandatory)]
        [string] $CuratedProjectPath,

        [Parameter(Mandatory)]
        [string] $StatePath,

        [object] $ActiveWeatherWorkers,

        [object] $WeatherBacklog,

        [string] $WeatherProgressPath,

        [ValidateRange(1, 60)]
        [int] $CuratedDrainIntervalMinutes = 8
    )

    $workers = [System.Collections.Generic.List[object]]::new()
    foreach ($month in $Months) {
        $monthKey = $month.From.Substring(0, 7)
        $stdoutPath = Join-Path $StatePath "raw-$monthKey.stdout.log"
        $stderrPath = Join-Path $StatePath "raw-$monthKey.stderr.log"
        $arguments = [System.Collections.Generic.List[string]]::new()
        foreach ($argument in @(
            'run',
            '--no-build',
            '--project',
            (ConvertTo-ProcessArgument $RawProjectPath),
            '--',
            '--mode',
            'raw',
            '--from',
            $month.From,
            '--to',
            $month.To,
            '--dispatch-item',
            ('year-{0}:{1}' -f $month.From.Substring(0, 4), $monthKey))) {
            $arguments.Add($argument)
        }
        if ($month.ReuseSuccessful) {
            $arguments.Add('--reuse-successful')
        }

        Write-Host (
            '  Starting Raw runner {0} for {1} through {2}.' -f
            ($workers.Count + 1),
            $month.From,
            $month.To)
        $process = Start-Process `
            -FilePath 'dotnet' `
            -ArgumentList $arguments `
            -WorkingDirectory ([System.IO.Path]::GetDirectoryName($RawProjectPath)) `
            -WindowStyle Hidden `
            -RedirectStandardOutput $stdoutPath `
            -RedirectStandardError $stderrPath `
            -PassThru
        $workers.Add([pscustomobject]@{
            Month = $month
            Process = $process
            StandardOutputPath = $stdoutPath
            StandardErrorPath = $stderrPath
        })
    }

    $pendingWorkers = [System.Collections.Generic.List[object]]::new()
    foreach ($worker in $workers) {
        $pendingWorkers.Add($worker)
    }

    $rawResults = [System.Collections.Generic.List[object]]::new()
    $weatherFailures = [System.Collections.Generic.List[object]]::new()
    $curatedExitCode = 0
    $nextCuratedDrainAt = [datetime]::UtcNow.AddMinutes($CuratedDrainIntervalMinutes)
    while ($pendingWorkers.Count -gt 0) {
        $completedWorkers = [System.Collections.Generic.List[object]]::new()
        foreach ($worker in $pendingWorkers.ToArray()) {
            if (-not $worker.Process.HasExited) {
                continue
            }

            $rawExitCode = $worker.Process.ExitCode
            if (Test-Path -LiteralPath $worker.StandardOutputPath) {
                Get-Content -LiteralPath $worker.StandardOutputPath | ForEach-Object {
                    Write-Host ('  [{0}] {1}' -f $worker.Month.From.Substring(0, 7), $_)
                }
            }
            if (Test-Path -LiteralPath $worker.StandardErrorPath) {
                Get-Content -LiteralPath $worker.StandardErrorPath | ForEach-Object {
                    Write-Warning ('[{0}] {1}' -f $worker.Month.From.Substring(0, 7), $_)
                }
            }

            Write-Host (
                '  [{0}] Raw runner exited {1}.' -f
                $worker.Month.From.Substring(0, 7),
                $rawExitCode)
            $rawResults.Add([pscustomobject]@{
                Month = $worker.Month
                RawExitCode = $rawExitCode
            })
            $completedWorkers.Add($worker)
        }

        foreach ($worker in $completedWorkers) {
            [void] $pendingWorkers.Remove($worker)
            $worker.Process.Dispose()
        }

        $drainReason = if ($completedWorkers.Count -gt 0) {
            '{0} Raw {1} completed' -f
                $completedWorkers.Count,
                $(if ($completedWorkers.Count -eq 1) { 'runner' } else { 'runners' })
        }
        elseif ([datetime]::UtcNow -ge $nextCuratedDrainAt) {
            'periodic live-data checkpoint'
        }
        else {
            $null
        }

        if ($null -ne $drainReason) {
            $drainExitCode = Invoke-SerializedCuratedDrain `
                -CuratedProjectPath $CuratedProjectPath `
                -Reason $drainReason
            $curatedExitCode = $drainExitCode
            $nextCuratedDrainAt = [datetime]::UtcNow.AddMinutes($CuratedDrainIntervalMinutes)
        }

        if ($null -ne $ActiveWeatherWorkers -and
            $null -ne $WeatherBacklog -and
            -not [string]::IsNullOrWhiteSpace($WeatherProgressPath) -and
            $weatherFailures.Count -eq 0) {
            foreach ($failure in @(Update-RaceWeatherPipeline `
                -ActiveWorkers $ActiveWeatherWorkers `
                -Backlog $WeatherBacklog `
                -RawProjectPath $RawProjectPath `
                -StatePath $StatePath `
                -ProgressPath $WeatherProgressPath)) {
                $weatherFailures.Add($failure)
            }
        }

        if ($pendingWorkers.Count -gt 0) {
            Start-Sleep -Seconds 2
        }
    }

    return @($rawResults | ForEach-Object {
        [pscustomobject]@{
            Month = $_.Month
            RawExitCode = $_.RawExitCode
            CuratedExitCode = $curatedExitCode
            WeatherFailures = @($weatherFailures)
        }
    })
}
