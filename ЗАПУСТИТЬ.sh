#!/bin/bash
# Скрипт для запуска C# версии на macOS через Mono

# Переходим в директорию скрипта
cd "$(dirname "$0")"

echo "════════════════════════════════════════════════════"
echo "   ЗАПУСК ПРОГРАММЫ ДИФРАКЦИИ (macOS)"
echo "════════════════════════════════════════════════════"

# Проверка наличия Mono
if ! command -v mono &> /dev/null; then
    echo "Ошибка: Mono не установлен. Пожалуйста, установите Mono Framework."
    echo "https://www.mono-project.com/download/stable/"
    exit 1
fi

# Проверка наличия скомпилированного файла
EXE_PATH="bin/Debug/Diffraction.exe"

if [ ! -f "$EXE_PATH" ]; then
    echo "[!] Файл не найден. Попытка компиляции..."
    if command -v msbuild &> /dev/null; then
        msbuild Diffraction.csproj /p:Configuration=Debug
    else
        echo "Ошибка: msbuild не найден. Не удалось собрать проект."
        exit 1
    fi
fi

if [ -f "$EXE_PATH" ]; then
    echo "[✓] Запуск программы через Mono..."
    mono "$EXE_PATH"
else
    echo "[✗] ОШИБКА: Не удалось найти или собрать $EXE_PATH"
    exit 1
fi
