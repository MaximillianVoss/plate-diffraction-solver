@echo off
chcp 65001 > nul
echo ════════════════════════════════════════════════════
echo   ОЧИСТКА И СБОРКА ПРОЕКТА
echo ════════════════════════════════════════════════════
echo.

echo [1/3] Очистка временных файлов...
if exist bin rmdir /s /q bin
if exist obj rmdir /s /q obj
if exist .vs rmdir /s /q .vs
echo      ✓ Временные файлы удалены
echo.

echo [2/3] Создание директорий...
mkdir bin\Debug 2>nul
mkdir obj\Debug 2>nul
echo      ✓ Директории созданы
echo.

echo [3/3] Компиляция проекта...
echo.

REM Ищем MSBuild
set MSBUILD_PATH=

REM Проверяем Visual Studio 2022
if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" set MSBUILD_PATH=%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe
if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" set MSBUILD_PATH=%ProgramFiles%\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe
if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe" set MSBUILD_PATH=%ProgramFiles%\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe

REM Проверяем Visual Studio 2019
if "%MSBUILD_PATH%"=="" if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe" set MSBUILD_PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe
if "%MSBUILD_PATH%"=="" if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe" set MSBUILD_PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe

REM Проверяем Visual Studio 2017
if "%MSBUILD_PATH%"=="" if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe" set MSBUILD_PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe

REM Проверяем старые версии .NET Framework
if "%MSBUILD_PATH%"=="" if exist "%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe" set MSBUILD_PATH=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe
if "%MSBUILD_PATH%"=="" if exist "%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe" set MSBUILD_PATH=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe

if "%MSBUILD_PATH%"=="" (
    echo [✗] ОШИБКА: MSBuild не найден!
    echo.
    echo РЕШЕНИЕ:
    echo 1. Установите .NET Framework 4.7.2 или выше
    echo 2. Или установите Visual Studio
    echo.
    pause
    exit /b 1
)

echo Используется: %MSBUILD_PATH%
echo.
"%MSBUILD_PATH%" Diffraction.csproj /p:Configuration=Debug /p:Platform=AnyCPU /t:Clean,Build /verbosity:minimal

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ════════════════════════════════════════════════════
    echo   ✓ КОМПИЛЯЦИЯ УСПЕШНА!
    echo ════════════════════════════════════════════════════
    echo.
    echo Запуск программы...
    echo.
    start "" "bin\Debug\Diffraction.exe"
) else (
    echo.
    echo ════════════════════════════════════════════════════
    echo   ✗ ОШИБКА КОМПИЛЯЦИИ!
    echo ════════════════════════════════════════════════════
    echo.
    echo РЕШЕНИЕ: Переместите проект в путь БЕЗ кириллицы:
    echo   Например: C:\Projects\Diffraction\
    echo.
    echo Текущий путь содержит кириллицу или спецсимволы.
    echo.
)

echo.
pause

