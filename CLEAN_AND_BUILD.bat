@echo off
cd /d "%~dp0"

echo ========================================================
echo   CLEAN AND BUILD PROJECT
echo ========================================================
echo.

echo [1/3] Cleaning temporary files...
if exist bin rmdir /s /q bin
if exist obj rmdir /s /q obj
if exist .vs rmdir /s /q .vs
echo      [OK] Temporary files deleted
echo.

echo [2/3] Creating directories...
mkdir bin\Debug 2>nul
mkdir obj\Debug 2>nul
echo      [OK] Directories created
echo.

echo [3/3] Compiling project...
echo.

REM Find MSBuild
set MSBUILD_PATH=

if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" set MSBUILD_PATH=%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe
if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" set MSBUILD_PATH=%ProgramFiles%\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe

if "%MSBUILD_PATH%"=="" if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe" set MSBUILD_PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe

if "%MSBUILD_PATH%"=="" if exist "%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe" set MSBUILD_PATH=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe
if "%MSBUILD_PATH%"=="" if exist "%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe" set MSBUILD_PATH=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe

if "%MSBUILD_PATH%"=="" (
    echo [ERROR] MSBuild not found!
    echo.
    echo SOLUTION:
    echo 1. Install .NET Framework 4.7.2 or higher
    echo 2. Or install Visual Studio
    echo https://dotnet.microsoft.com/download/dotnet-framework/net472
    echo.
    pause
    exit /b 1
)

echo Using: %MSBUILD_PATH%
echo.
"%MSBUILD_PATH%" Diffraction.csproj /p:Configuration=Debug /p:Platform=AnyCPU /t:Clean,Build /verbosity:minimal

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================================
    echo   [SUCCESS] COMPILATION COMPLETE!
    echo ========================================================
    echo.
    echo Starting program...
    echo.
    start "" "bin\Debug\Diffraction.exe"
) else (
    echo.
    echo ========================================================
    echo   [ERROR] COMPILATION FAILED!
    echo ========================================================
    echo.
    echo SOLUTION: Move project to path WITHOUT Cyrillic:
    echo   Example: C:\Projects\Diffraction\
    echo.
    echo Current path may contain Cyrillic or special characters.
    echo.
)

echo.
pause
