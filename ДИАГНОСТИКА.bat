@echo off
chcp 65001 >nul
cd /d "%~dp0"

echo ════════════════════════════════════════════════════
echo   ДИАГНОСТИКА ОКРУЖЕНИЯ
echo ════════════════════════════════════════════════════
echo.

echo [1] Проверка текущего каталога:
echo    %CD%
echo.

echo [2] Проверка наличия файла проекта:
if exist "Diffraction.csproj" (
    echo    ✓ Diffraction.csproj найден
) else (
    echo    ✗ Diffraction.csproj НЕ НАЙДЕН!
)
echo.

echo [3] Проверка структуры каталогов:
if exist "Properties" (
    echo    ✓ Properties\
) else (
    echo    ✗ Properties\ - НЕ НАЙДЕНА
)
if exist "Program.cs" (
    echo    ✓ Program.cs
) else (
    echo    ✗ Program.cs - НЕ НАЙДЕН
)
if exist "Form1.cs" (
    echo    ✓ Form1.cs
) else (
    echo    ✗ Form1.cs - НЕ НАЙДЕН
)
echo.

echo [4] Проверка скомпилированных файлов:
if exist "bin\Debug\Diffraction.exe" (
    echo    ✓ bin\Debug\Diffraction.exe существует
    dir "bin\Debug\Diffraction.exe" | find "Diffraction.exe"
) else (
    echo    ✗ bin\Debug\Diffraction.exe НЕ НАЙДЕН
    if exist "bin" (
        echo    Папка bin\ существует
        if exist "bin\Debug" (
            echo    Папка bin\Debug\ существует
            dir /b "bin\Debug"
        ) else (
            echo    Папка bin\Debug\ НЕ СУЩЕСТВУЕТ
        )
    ) else (
        echo    Папка bin\ НЕ СУЩЕСТВУЕТ
    )
)
echo.

echo [5] Поиск MSBuild:
set MSBUILD_PATH=

REM Visual Studio 2022
if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" (
    set MSBUILD_PATH=%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe
    echo    ✓ VS 2022 Community найден
    goto :msbuild_found
)
if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" (
    set MSBUILD_PATH=%ProgramFiles%\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe
    echo    ✓ VS 2022 Professional найден
    goto :msbuild_found
)

REM Visual Studio 2019
if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe" (
    set MSBUILD_PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe
    echo    ✓ VS 2019 Community найден
    goto :msbuild_found
)

REM .NET Framework
if exist "%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe" (
    set MSBUILD_PATH=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe
    echo    ✓ .NET Framework 4.0 (64-bit) найден
    goto :msbuild_found
)
if exist "%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe" (
    set MSBUILD_PATH=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe
    echo    ✓ .NET Framework 4.0 (32-bit) найден
    goto :msbuild_found
)

echo    ✗ MSBuild НЕ НАЙДЕН
echo.
echo    РЕШЕНИЕ: Установите одно из:
echo    - Visual Studio 2019 или 2022 (Community бесплатная)
echo    - .NET Framework 4.7.2 Developer Pack
echo.
goto :skip_msbuild

:msbuild_found
echo.
echo [6] Путь к MSBuild:
echo    %MSBUILD_PATH%
echo.

echo [7] Версия MSBuild:
"%MSBUILD_PATH%" /version /nologo
echo.

:skip_msbuild

echo [8] Проверка .NET Framework:
reg query "HKLM\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" /v Release 2>nul | find "Release"
echo.

echo [9] Проверка пути (кириллица):
echo    %CD% | findstr /R "[А-Яа-яЁё]" >nul
if errorlevel 1 (
    echo    ✓ Путь не содержит кириллицу
) else (
    echo    ✗ ВНИМАНИЕ: Путь содержит кириллицу!
    echo    Это может вызвать проблемы с компиляцией.
    echo    Переместите проект в путь вида: C:\Projects\Diffraction\
)
echo.

echo ════════════════════════════════════════════════════
echo   ДИАГНОСТИКА ЗАВЕРШЕНА
echo ════════════════════════════════════════════════════
echo.
echo Нажмите любую клавишу для продолжения...
pause >nul
