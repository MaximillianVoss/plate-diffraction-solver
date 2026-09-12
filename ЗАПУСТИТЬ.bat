@echo off
cd /d "%~dp0"

where dotnet >nul 2>&1
if errorlevel 1 (
    echo .NET 9 SDK was not found.
    echo Install .NET 9 SDK and run this file again.
    pause
    exit /b 1
)

echo Building the current single-plate WPF application...
dotnet build "Diffraction.WpfPrototype\Diffraction.WpfPrototype.csproj" -c Release --nologo
if errorlevel 1 (
    echo.
    echo Build failed. See the error above.
    pause
    exit /b 1
)

echo Starting the application...
start "" "Diffraction.WpfPrototype\bin\Release\net9.0-windows\Diffraction.WpfPrototype.exe"
exit /b 0
