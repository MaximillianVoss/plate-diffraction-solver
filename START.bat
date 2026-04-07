@echo off
cd /d "%~dp0"

echo ========================================================
echo   DIFFRACTION PROGRAM LAUNCHER
echo ========================================================
echo.
echo Current directory: %CD%
echo.

REM Close any running instances to avoid file locking
echo Checking for running instances...
tasklist /FI "IMAGENAME eq Diffraction.exe" 2>NUL | find /I /N "Diffraction.exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo [!] Closing running instances of Diffraction.exe...
    taskkill /F /IM Diffraction.exe >NUL 2>&1
    timeout /t 1 /nobreak >NUL
    echo [OK] Closed
)
echo.

echo ========================================================
echo   COMPILING PROJECT
echo ========================================================
echo.

REM Create directories if needed
if not exist "bin\Debug" mkdir "bin\Debug"
if not exist "obj\Debug" mkdir "obj\Debug"

REM Find MSBuild
set MSBUILD_PATH=

REM Check Visual Studio 2022
if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" (
    set MSBUILD_PATH=%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe
)
if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" (
    set MSBUILD_PATH=%ProgramFiles%\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe
)
if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe" (
    set MSBUILD_PATH=%ProgramFiles%\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe
)

REM Check Visual Studio 2019
if "%MSBUILD_PATH%"=="" (
    if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe" (
        set MSBUILD_PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe
    )
    if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe" (
        set MSBUILD_PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe
    )
)

REM Check Visual Studio 2017
if "%MSBUILD_PATH%"=="" (
    if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe" (
        set MSBUILD_PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe
    )
    if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin\MSBuild.exe" (
        set MSBUILD_PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin\MSBuild.exe
    )
)

REM Check .NET Framework
if "%MSBUILD_PATH%"=="" (
    if exist "%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe" (
        set MSBUILD_PATH=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe
    )
)
if "%MSBUILD_PATH%"=="" (
    if exist "%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe" (
        set MSBUILD_PATH=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe
    )
)

if "%MSBUILD_PATH%"=="" (
    echo [ERROR] MSBuild not found!
    echo.
    echo SOLUTION:
    echo 1. Run DIAGNOSTIC.bat for detailed check
    echo 2. Install .NET Framework 4.7.2 or higher
    echo 3. Or install Visual Studio 2017/2019/2022
    echo.
    echo Download .NET Framework 4.7.2:
    echo https://dotnet.microsoft.com/download/dotnet-framework/net472
    echo.
    pause
    exit /b 1
)

echo Using MSBuild: %MSBUILD_PATH%
echo.
echo Compiling project (may take 10-30 seconds)...
echo.

"%MSBUILD_PATH%" Diffraction.csproj /p:Configuration=Debug /t:Build /verbosity:normal /nologo

if errorlevel 1 (
    echo.
    echo ========================================================
    echo   [ERROR] COMPILATION FAILED!
    echo ========================================================
    echo.
    echo Current directory: %CD%
    echo.
    echo POSSIBLE SOLUTIONS:
    echo.
    echo 1. Move project to path WITHOUT Cyrillic characters:
    echo    BAD:  C:\Users\Denis\Downloads\Diffraction-main(1)\
    echo    GOOD: C:\Projects\Diffraction\
    echo.
    echo 2. Install .NET Framework 4.7.2 Developer Pack:
    echo    https://dotnet.microsoft.com/download/dotnet-framework
    echo.
    echo 3. Delete bin\ and obj\ folders manually, then retry
    echo.
    echo 4. Open Diffraction.csproj in Visual Studio and build manually
    echo.
    echo 5. Run DIAGNOSTIC.bat for detailed diagnostics
    echo.
    pause
    exit /b 1
)

REM Verify that exe was created
if not exist "bin\Debug\Diffraction.exe" (
    echo.
    echo ========================================================
    echo   [ERROR] EXE file not created!
    echo ========================================================
    echo.
    echo Compilation finished but Diffraction.exe not found.
    echo Check compilation output above for errors.
    echo.
    pause
    exit /b 1
)

echo.
echo ========================================================
echo   [SUCCESS] COMPILATION COMPLETE!
echo ========================================================
echo.
echo Starting program...
start "" "bin\Debug\Diffraction.exe"
