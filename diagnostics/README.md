# Diagnostics

CSV-файлы в этой папке получены после исправления матричного члена условия Леонтовича для двух пластин.

Файлы:
- `accuracy_sweep_angle10_fixed.csv` - прогон для угла 10 градусов, `N = 5, 10, 20, 30, 60`, `skinDepth = 0, 0.1, 0.01, 0.001`.
- `accuracy_sweep_angle10_fixed_compare.csv` - сравнение тех же расчетов со случаем `skinDepth = 0`.
- `accuracy_sweep_quick_fixed.csv` - быстрый прогон для углов `0, 30, 60, 90`, `N = 5, 10, 20, 30, 60`.
- `accuracy_sweep_quick_fixed_compare.csv` - сравнение быстрого прогона со случаем `skinDepth = 0`.

Повторить быстрый прогон:

```powershell
dotnet build Diffraction.sln -c Debug
Start-Process -FilePath ".\bin\Debug\Diffraction.exe" -WorkingDirectory (Get-Location) -ArgumentList "--accuracy-sweep-quick --output .\diagnostics\accuracy_sweep_quick_fixed.csv" -Wait
```

Повторить прогон для конкретных параметров:

```powershell
Start-Process -FilePath ".\bin\Debug\Diffraction.exe" -WorkingDirectory (Get-Location) -ArgumentList "--accuracy-sweep --output .\diagnostics\accuracy_sweep_angle10_fixed.csv --angles 10 --n-values 5,10,20,30,60 --skins 0,0.1,0.01,0.001" -Wait
```

Важно: текущая энергетическая диагностика в приложении не является строгой проверкой закона сохранения энергии. Она оценивает отраженную энергию по потоку через одну контрольную вертикаль, а не через замкнутый контур или дальнее поле, поэтому может быть немонотонной по `N`.

Если опорная падающая энергия для этой контрольной поверхности близка к нулю, энергетические проценты записываются как `NaN`; для таких строк нужно смотреть прежде всего `BcErrorPct` и `HelmholtzResidual`.
