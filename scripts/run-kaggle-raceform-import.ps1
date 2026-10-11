[CmdletBinding()]
param(
    [Parameter()]
    [datetime]$From = [datetime]'2015-01-01',

    [Parameter()]
    [datetime]$To = (Get-Date),

    [Parameter()]
    [switch]$RawOnly,

    [Parameter()]
    [switch]$RefreshRaw
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot 'src\HorseRacing.KaggleDataSync\HorseRacing.KaggleDataSync.csproj'
$arguments = @(
    'run',
    '--project', $project,
    '--configuration', 'Release',
    '--',
    '--from', $From.ToString('yyyy-MM-dd'),
    '--to', $To.ToString('yyyy-MM-dd')
)

if ($RawOnly) {
    $arguments += '--raw-only'
}

if ($RefreshRaw) {
    $arguments += '--refresh-raw'
}

Push-Location $repositoryRoot
try {
    & dotnet @arguments
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
