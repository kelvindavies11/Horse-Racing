Set-StrictMode -Version Latest

function ConvertTo-ProcessArgument([string] $Value) {
    if ($Value -notmatch '[\s"]') {
        return $Value
    }

    return '"' + $Value.Replace('"', '\"') + '"'
}

function Invoke-ParallelRawBatch {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [object[]] $Months,

        [Parameter(Mandatory)]
        [string] $RawProjectPath,

        [Parameter(Mandatory)]
        [string] $StatePath
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
            $month.To)) {
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

    $results = [System.Collections.Generic.List[object]]::new()
    foreach ($worker in $workers) {
        $worker.Process.WaitForExit()
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

        $results.Add([pscustomobject]@{
            Month = $worker.Month
            ExitCode = $worker.Process.ExitCode
        })
        $worker.Process.Dispose()
    }

    return $results.ToArray()
}
