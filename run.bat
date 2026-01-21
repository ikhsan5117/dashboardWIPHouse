@echo off
REM ============================================
REM Dashboard WIP House - Quick Start
REM ============================================

title Dashboard WIP House

echo ============================================
echo   Dashboard WIP House - Quick Start
echo ============================================
echo.

REM Check if dotnet exists
if not exist "C:\Program Files\dotnet\dotnet.exe" (
    echo ERROR: .NET SDK not found!
    echo.
    echo Please install .NET SDK 8.0 from:
    echo https://dotnet.microsoft.com/download/dotnet/8.0
    echo.
    pause
    exit /b 1
)

echo [OK] .NET SDK found
echo.

REM Get dotnet version
for /f "delims=" %%i in ('"C:\Program Files\dotnet\dotnet.exe" --version') do set DOTNET_VERSION=%%i
echo .NET Version: %DOTNET_VERSION%
echo.

REM Ask for profile
echo Choose profile:
echo.
echo 1. HTTP  - http://localhost:5005
echo 2. HTTPS - https://localhost:7160 (for tablet/camera)
echo.

set /p CHOICE="Enter choice (1 or 2) [default: 2]: "

if "%CHOICE%"=="" set CHOICE=2

if "%CHOICE%"=="1" (
    set PROFILE=http
    set URL=http://localhost:5005
) else (
    set PROFILE=https
    set URL=https://localhost:7160
)

echo.
echo ============================================
echo   Starting Application...
echo ============================================
echo.
echo Profile: %PROFILE%
echo URL: %URL%
echo.
echo Press Ctrl+C to stop
echo.
echo ============================================
echo.

REM Run the application
cd /d "%~dp0"
"C:\Program Files\dotnet\dotnet.exe" run --launch-profile %PROFILE%

if errorlevel 1 (
    echo.
    echo ERROR: Failed to start application
    echo.
    pause
    exit /b 1
)
