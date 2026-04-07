@echo off
chcp 65001 >nul
cd /d "%~dp0"

echo ════════════════════════════════════════════════════
echo   ЗАПУСК ПРОГРАММЫ ДИФРАКЦИИ
echo ════════════════════════════════════════════════════
echo.
echo Рабочий каталог: %CD%
echo.

REM Проверяем наличие скомпилированного файла
if exist "bin\Debug\Diffraction.exe" (
    echo [✓] Найден скомпилированный файл
    echo.
    echo Запуск программы...
    start "" "bin\Debug\Diffraction.exe"
    exit /b 0
)

echo [!] Программа не скомпилирована
echo.
echo ════════════════════════════════════════════════════
echo   КОМПИЛЯЦИЯ ПРОЕКТА
echo ════════════════════════════════════════════════════
echo.

REM Создаем директории если их нет
if not exist "bin\Debug" mkdir "bin\Debug"
if not exist "obj\Debug" mkdir "obj\Debug"

REM Ищем MSBuild (пробуем разные пути)
set MSBUILD_PATH=

REM Проверяем Visual Studio 2022
if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" (
    set MSBUILD_PATH=%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe
)
if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" (
    set MSBUILD_PATH=%ProgramFiles%\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe
)
if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe" (
    set MSBUILD_PATH=%ProgramFiles%\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe
)

REM Проверяем Visual Studio 2019
if "%MSBUILD_PATH%"=="" (
    if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe" (
        set MSBUILD_PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe
    )
    if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe" (
        set MSBUILD_PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe
    )
)

REM Проверяем Visual Studio 2017
if "%MSBUILD_PATH%"=="" (
    if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe" (
        set MSBUILD_PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe
    )
    if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin\MSBuild.exe" (
        set MSBUILD_PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin\MSBuild.exe
    )
)

REM Проверяем старые версии .NET Framework
if "%MSBUILD_PATH%"=="" (
    for %%v in (v4.0.30319) do (
        if exist "%SystemRoot%\Microsoft.NET\Framework64\%%v\MSBuild.exe" (
            set MSBUILD_PATH=%SystemRoot%\Microsoft.NET\Framework64\%%v\MSBuild.exe
        )
        if "%MSBUILD_PATH%"=="" (
            if exist "%SystemRoot%\Microsoft.NET\Framework\%%v\MSBuild.exe" (
                set MSBUILD_PATH=%SystemRoot%\Microsoft.NET\Framework\%%v\MSBuild.exe
            )
        )
    )
)

if "%MSBUILD_PATH%"=="" (
    echo [✗] ОШИБКА: MSBuild не найден!
    echo.
    echo РЕШЕНИЕ:
    echo 1. Запустите ДИАГНОСТИКА.bat для подробной проверки
    echo 2. Установите .NET Framework 4.7.2 или выше
    echo 3. Или установите Visual Studio 2017/2019/2022
    echo.
    echo Хотите запустить диагностику? (Y/N)
    choice /C YN /N /M "Выберите Y или N: "
    if errorlevel 2 goto :no_diag
    if errorlevel 1 (
        start "" "ДИАГНОСТИКА.bat"
        exit /b 1
    )
    :no_diag
    pause
    exit /b 1
)

echo Используется MSBuild: %MSBUILD_PATH%
echo.
echo Компиляция проекта (это может занять 10-30 секунд)...
echo.

"%MSBUILD_PATH%" Diffraction.csproj /p:Configuration=Debug /t:Build /verbosity:normal /nologo

if errorlevel 1 (
    echo.
    echo ════════════════════════════════════════════════════
    echo   [✗] ОШИБКА КОМПИЛЯЦИИ!
    echo ════════════════════════════════════════════════════
    echo.
    echo Текущий каталог: %CD%
    echo.
    echo ВОЗМОЖНЫЕ РЕШЕНИЯ:
    echo.
    echo 1. Переместите проект в путь БЕЗ кириллицы и скобок:
    echo    Плохо:  C:\Users\Дима\Downloads\Diffraction-main(1)\
    echo    Хорошо: C:\Projects\Diffraction\
    echo.
    echo 2. Установите .NET Framework 4.7.2 Developer Pack:
    echo    https://dotnet.microsoft.com/download/dotnet-framework
    echo.
    echo 3. Запустите скрипт: ОЧИСТИТЬ_И_СОБРАТЬ.bat
    echo.
    echo 4. Удалите папки bin\ и obj\ вручную, затем повторите запуск
    echo.
    echo 5. Откройте Diffraction.csproj в Visual Studio и соберите вручную
    echo.
    echo 6. Запустите ДИАГНОСТИКА.bat для подробной проверки
    echo.
    echo Хотите запустить диагностику? (Y/N)
    choice /C YN /N /M "Выберите Y или N: " /T 10 /D N
    if errorlevel 2 goto :skip_diag_compile
    if errorlevel 1 start "" "ДИАГНОСТИКА.bat"
    :skip_diag_compile
    pause
    exit /b 1
)

REM Проверяем, что exe действительно создан
if not exist "bin\Debug\Diffraction.exe" (
    echo.
    echo ════════════════════════════════════════════════════
    echo   [✗] ОШИБКА: EXE файл не создан!
    echo ════════════════════════════════════════════════════
    echo.
    echo Компиляция завершилась, но файл Diffraction.exe не найден.
    echo Проверьте вывод компиляции выше на наличие ошибок.
    echo.
    pause
    exit /b 1
)

echo.
echo ════════════════════════════════════════════════════
echo   [✓] КОМПИЛЯЦИЯ УСПЕШНА!
echo ════════════════════════════════════════════════════
echo.
echo Запуск программы...
start "" "bin\Debug\Diffraction.exe"
