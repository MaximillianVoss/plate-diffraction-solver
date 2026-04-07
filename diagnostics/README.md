# Diagnostics

CSV-файлы в этой папке получены после исправления матричного члена условия Леонтовича для двух пластин и пересчета энергетики через контрольный контур.

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

Важно: отраженная энергия теперь оценивается по рассеянному потоку через контрольный контур вокруг пластин. Прошедшая энергия восстанавливается из баланса `I - R - A`, чтобы отчет не показывал нефизичные огромные значения из локальной контрольной вертикали.

Падающая энергия нормируется по фиксированному расчетному окну вокруг пластин, поэтому углы около 90 градусов больше не дают деления на почти нулевую величину.
