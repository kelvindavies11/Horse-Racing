@echo off
setlocal

set "REPOSITORY_ROOT=%~dp0"
set "WEB_DIRECTORY=%REPOSITORY_ROOT%src\HorseRacing.Web"
set "API_PROJECT=src\HorseRacing.Api\HorseRacing.Api.csproj"
set "WEB_URL=http://127.0.0.1:5173"
set "OPEN_BROWSER=1"
if /I "%~1"=="--no-browser" set "OPEN_BROWSER=0"

where dotnet.exe >nul 2>&1
if errorlevel 1 (
    echo ERROR: The .NET SDK was not found on PATH.
    goto :failed
)

where npm.cmd >nul 2>&1
if errorlevel 1 (
    echo ERROR: npm was not found on PATH. Install Node.js 22 or later.
    goto :failed
)

if not exist "%REPOSITORY_ROOT%%API_PROJECT%" (
    echo ERROR: The HorseRacing.Api project was not found.
    goto :failed
)

if not exist "%WEB_DIRECTORY%\package.json" (
    echo ERROR: The HorseRacing.Web package was not found.
    goto :failed
)

if not exist "%WEB_DIRECTORY%\node_modules" (
    echo Installing website dependencies...
    pushd "%WEB_DIRECTORY%"
    call npm.cmd ci
    if errorlevel 1 (
        popd
        echo ERROR: Website dependency installation failed.
        goto :failed
    )
    popd
)

if not defined ConnectionStrings__HorseRacing set "ConnectionStrings__HorseRacing=Host=localhost;Port=5433;Database=horse_racing;Username=horse_racing;Password=horse_racing_local"

powershell.exe -NoProfile -Command "if (Get-NetTCPConnection -State Listen -LocalPort 5080 -ErrorAction SilentlyContinue) { exit 0 } else { exit 1 }"
if errorlevel 1 (
    echo Starting API at http://localhost:5080 ...
    start "Horse Racing API" /D "%REPOSITORY_ROOT%" cmd.exe /k dotnet run --project %API_PROJECT% --launch-profile http
) else (
    echo API port 5080 is already active; leaving the existing process running.
)

powershell.exe -NoProfile -Command "if (Get-NetTCPConnection -State Listen -LocalPort 5173 -ErrorAction SilentlyContinue) { exit 0 } else { exit 1 }"
if errorlevel 1 (
    echo Starting website at %WEB_URL% ...
    start "Horse Racing Web" /D "%WEB_DIRECTORY%" cmd.exe /k npm.cmd run dev
    timeout /t 4 /nobreak >nul
) else (
    echo Website port 5173 is already active; leaving the existing process running.
)

if "%OPEN_BROWSER%"=="1" (
    echo Opening %WEB_URL% ...
    start "" "%WEB_URL%"
) else (
    echo Website available at %WEB_URL%.
)
exit /b 0

:failed
echo.
echo The API and website were not started.
pause
exit /b 1
