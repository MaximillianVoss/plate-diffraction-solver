@echo off
cd /d "%~dp0"

echo ========================================================
echo   ENVIRONMENT DIAGNOSTIC
echo ========================================================
echo.

echo [1] Current directory:
echo    %CD%
echo.

echo [2] Checking project file:
if exist "Diffraction.csproj" (
    echo    [OK] Diffraction.csproj found
) else (
    echo    [ERROR] Diffraction.csproj NOT FOUND!
)
echo.

echo [3] Checking directory structure:
if exist "Properties" (
    echo    [OK] Properties\
) else (
    echo    [ERROR] Properties\ - NOT FOUND
)
if exist "Program.cs" (
    echo    [OK] Program.cs
) else (
    echo    [ERROR] Program.cs - NOT FOUND
)
if exist "Form1.cs" (
    echo    [OK] Form1.cs
) else (
    echo    [ERROR] Form1.cs - NOT FOUND
)
echo.

echo [4] Checking compiled files:
if exist "bin\Debug\Diffraction.exe" (
    echo    [OK] bin\Debug\Diffraction.exe exists
    dir "bin\Debug\Diffraction.exe" | find "Diffraction.exe"
) else (
    echo    [ERROR] bin\Debug\Diffraction.exe NOT FOUND
    if exist "bin" (
        echo    Folder bin\ exists
        if exist "bin\Debug" (
            echo    Folder bin\Debug\ exists
            echo    Contents:
            dir /b "bin\Debug"
        ) else (
            echo    Folder bin\Debug\ DOES NOT EXIST
        )
    ) else (
        echo    Folder bin\ DOES NOT EXIST
    )
)
echo.

echo [5] Searching for MSBuild:
set MSBUILD_PATH=

REM Visual Studio 2022
if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" (
    set MSBUILD_PATH=%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe
    echo    [OK] VS 2022 Community found
    goto :msbuild_found
)
if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" (
    set MSBUILD_PATH=%ProgramFiles%\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe
    echo    [OK] VS 2022 Professional found
    goto :msbuild_found
)

REM Visual Studio 2019
if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe" (
    set MSBUILD_PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe
    echo    [OK] VS 2019 Community found
    goto :msbuild_found
)

REM .NET Framework
if exist "%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe" (
    set MSBUILD_PATH=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe
    echo    [OK] .NET Framework 4.0 (64-bit) found
    goto :msbuild_found
)
if exist "%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe" (
    set MSBUILD_PATH=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe
    echo    [OK] .NET Framework 4.0 (32-bit) found
    goto :msbuild_found
)

echo    [ERROR] MSBuild NOT FOUND
echo.
echo    SOLUTION: Install one of:
echo    - Visual Studio 2019 or 2022 (Community is free)
echo    - .NET Framework 4.7.2 Developer Pack
echo    https://dotnet.microsoft.com/download/dotnet-framework/net472
echo.
goto :skip_msbuild

:msbuild_found
echo.
echo [6] MSBuild path:
echo    %MSBUILD_PATH%
echo.

echo [7] MSBuild version:
"%MSBUILD_PATH%" /version /nologo
echo.

:skip_msbuild

echo [8] Checking .NET Framework installation:
reg query "HKLM\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" /v Release 2>nul
if errorlevel 1 (
    echo    [ERROR] .NET Framework 4.x not found in registry
) else (
    echo    [OK] .NET Framework 4.x found
)
echo.

echo [9] Checking for Cyrillic characters in path:
echo %CD% | findstr /R "[^A-Za-z0-9:\\_ .()-]" >nul
if errorlevel 1 (
    echo    [OK] Path contains only safe characters
) else (
    echo    [WARNING] Path contains non-ASCII characters!
    echo    This may cause compilation issues.
    echo    Recommended: Move project to C:\Projects\Diffraction\
)
echo.

echo ========================================================
echo   DIAGNOSTIC COMPLETE
echo ========================================================
echo.
echo Press any key to continue...
pause >nul
