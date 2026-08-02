# Diagnostics

Актуальная энергетическая модель с 2026-08-02: тонкий импедансный лист с условием `u=qJ` и единой нормировкой поверхностного тока во всех вычислительных модулях.

Основные материалы:

- `energy-balance-research.md` - вывод модели, формулы потоков и численная проверка;
- `energy-formulas-sources.md` - физические источники и связь обозначений с кодом;
- `automated-testing-report.md` - результаты расширенного тестирования физики и пользовательской отчётности;
- `energy_sheet_validation.csv` - контрольный прогон рассеяния и ЗСЭ после исправления масштаба тока;
- `energy_sheet_validation_compare.csv` - сравнение тех же строк с идеальным листом.

Старые файлы `accuracy_sweep_*_fixed*.csv` и `skin_method_compare.*` были получены до перехода на условие тонкого листа. Они сохранены как история исследования и не должны использоваться как актуальные численные результаты.

## Повторить контрольный прогон

```powershell
dotnet build Diffraction.sln -c Debug
Start-Process -FilePath ".\bin\Debug\Diffraction.exe" -WorkingDirectory (Get-Location) -ArgumentList "--accuracy-sweep --output .\diagnostics\energy_sheet_validation.csv --angles 45 --n-values 30 --skins 0,0.001,0.01,0.05,0.1,0.2" -Wait
```

CSV содержит:

- падающую энергию только на проекции пластин;
- неотрицательные `ReflectedScatteredPct` и `ForwardScatteredPct` как основные энергии рассеянного поля;
- расхождение верхней и нижней половин дальнего поля;
- потоки рассеянного поля непосредственно сверху и снизу листа;
- `IncidentSideDeficitPct` и `OppositeSideSignedFluxPct` как локальные знаковые диагностические величины;
- поглощение через ток и через разность верхнего/нижнего потоков;
- сами знаковые потоки сверху и снизу;
- локальную невязку без перенормировки.

## Сравнение методов

```powershell
Start-Process -FilePath ".\bin\Debug\Diffraction.exe" -WorkingDirectory (Get-Location) -ArgumentList "--skin-method-compare --skin-depth 0.1 --n 30 --theta-deg 45 --output .\diagnostics\skin_method_compare.csv --image .\diagnostics\skin_method_compare.png" -Wait
```

В отчете интерфейса дополнительно вычисляется замкнутый знаковый контур. Отрицательный локальный поток больше не подписывается как прошедшая энергия: рассеянная вперёд компонента `T_scat` выводится отдельно и всегда неотрицательна.

## Полная автоматическая проверка

```powershell
.\scripts\run_validation.ps1
```

Команда собирает решение и нативный CPU-модуль, запускает все тесты, проверяет матрицу углов и толщин, контролирует контракт CSV и создаёт отчёт в `artifacts/validation/validation-report.md`. Та же команда выполняется в GitHub Actions при каждом обновлении репозитория.
