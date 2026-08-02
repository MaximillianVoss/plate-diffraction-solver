param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$OutputDirectory,
    [switch]$SkipNativeBuild
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot 'artifacts\validation'
}
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$trxPath = Join-Path $OutputDirectory 'automated-tests.trx'
$sweepPath = Join-Path $OutputDirectory 'energy-behavior.csv'
$comparePath = Join-Path $OutputDirectory 'energy-behavior_compare.csv'
$reportPath = Join-Path $OutputDirectory 'validation-report.md'

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock]$Command,
        [Parameter(Mandatory = $true)]
        [string]$FailureMessage
    )

    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$FailureMessage (exit code $LASTEXITCODE)"
    }
}

function Parse-InvariantDouble {
    param([string]$Value)

    return [double]::Parse(
        $Value,
        [System.Globalization.NumberStyles]::Float,
        [System.Globalization.CultureInfo]::InvariantCulture)
}

Push-Location $repoRoot
try {
    Invoke-CheckedCommand -FailureMessage 'Solution build failed' -Command {
        dotnet build Diffraction.sln -c $Configuration -v minimal
    }

    if (-not $SkipNativeBuild) {
        Invoke-CheckedCommand -FailureMessage 'Native CPU build failed' -Command {
            & (Join-Path $repoRoot 'Diffraction.Cpp\build_cpu.ps1')
        }
    }

    Invoke-CheckedCommand -FailureMessage 'Automated tests failed' -Command {
        dotnet test Diffraction.Tests\Diffraction.Tests.csproj `
            -c $Configuration `
            --no-build `
            --no-restore `
            -v minimal `
            --logger "trx;LogFileName=$([System.IO.Path]::GetFileName($trxPath))" `
            --results-directory $OutputDirectory
    }

    $applicationPath = Join-Path $repoRoot "bin\$Configuration\Diffraction.exe"
    $sweepArguments = @(
        '--accuracy-sweep',
        '--output', "`"$sweepPath`"",
        '--angles', '15,30,45,60,90',
        '--n-values', '30',
        '--skins', '0,0.001,0.01,0.05,0.1,0.2'
    )
    $sweepProcess = Start-Process `
        -FilePath $applicationPath `
        -WorkingDirectory $repoRoot `
        -ArgumentList $sweepArguments `
        -PassThru `
        -Wait
    if ($sweepProcess.ExitCode -ne 0) {
        throw "Energy behavior sweep failed (exit code $($sweepProcess.ExitCode))"
    }

    $rows = @(Import-Csv -LiteralPath $sweepPath -Delimiter ';')
    $failures = [System.Collections.Generic.List[string]]::new()
    $expectedHeaders = @(
        'ThetaDeg', 'N', 'SkinDepth', 'SolveResult', 'BcErrorPct', 'HelmholtzResidual',
        'PlateIncident', 'ReflectedScatteredPct', 'ForwardScatteredPct', 'ScatteredTotalPct',
        'ScatteredHalfPlaneMismatchPct', 'SheetScatteredAbovePct', 'SheetScatteredBelowPct',
        'SheetScatteredMismatchPct', 'IncidentSideDeficitPct', 'OppositeSideSignedFluxPct',
        'AbsorbedCurrentPct', 'AbsorbedFluxPct', 'AboveTotalFluxPct', 'BelowTotalFluxPct',
        'PlateBalanceTotalPct', 'LocalBalanceErrorPct', 'SolveTimeMs', 'MetricsTimeMs', 'ErrorMessage'
    )

    if ($rows.Count -ne 30) {
        $failures.Add("Expected 30 sweep rows, got $($rows.Count).")
    }
    if ($rows.Count -gt 0) {
        $actualHeaders = @($rows[0].PSObject.Properties.Name)
        if (($expectedHeaders -join ';') -ne ($actualHeaders -join ';')) {
            $failures.Add('CSV column contract differs from the documented schema.')
        }
    }

    $maxFarMismatchPct = 0.0
    $maxSheetMismatchPct = 0.0
    $maxLocalBalanceErrorPct = 0.0
    $minimumScatteredPct = [double]::PositiveInfinity

    foreach ($row in $rows) {
        $caseName = "theta=$($row.ThetaDeg), N=$($row.N), skin=$($row.SkinDepth)"
        if ($row.SolveResult -ne '1') {
            $failures.Add("${caseName}: solver result is $($row.SolveResult); $($row.ErrorMessage)")
            continue
        }

        try {
            $incident = Parse-InvariantDouble $row.PlateIncident
            $reflected = Parse-InvariantDouble $row.ReflectedScatteredPct
            $forward = Parse-InvariantDouble $row.ForwardScatteredPct
            $farMismatch = [Math]::Abs((Parse-InvariantDouble $row.ScatteredHalfPlaneMismatchPct))
            $sheetAbove = Parse-InvariantDouble $row.SheetScatteredAbovePct
            $sheetBelow = Parse-InvariantDouble $row.SheetScatteredBelowPct
            $sheetMismatch = [Math]::Abs((Parse-InvariantDouble $row.SheetScatteredMismatchPct))
            $absorption = Parse-InvariantDouble $row.AbsorbedCurrentPct
            $localBalance = [Math]::Abs((Parse-InvariantDouble $row.LocalBalanceErrorPct))

            if ($incident -le 0.0) { $failures.Add("${caseName}: projected incident energy is not positive.") }
            if ($reflected -lt -1e-9) { $failures.Add("${caseName}: R_scat is negative ($reflected%).") }
            if ($forward -lt -1e-9) { $failures.Add("${caseName}: T_scat is negative ($forward%).") }
            if ($sheetAbove -lt -1e-9 -or $sheetBelow -lt -1e-9) {
                $failures.Add("${caseName}: scattered sheet flux is negative.")
            }
            if ($absorption -lt -1e-8) { $failures.Add("${caseName}: absorption is negative ($absorption%).") }
            if ($farMismatch -ge 1e-6) { $failures.Add("${caseName}: far half-plane mismatch is $farMismatch%.") }
            if ($sheetMismatch -ge 1e-6) { $failures.Add("${caseName}: sheet flux mismatch is $sheetMismatch%.") }
            if ($localBalance -ge 2.0) { $failures.Add("${caseName}: local balance error is $localBalance%.") }

            $maxFarMismatchPct = [Math]::Max($maxFarMismatchPct, $farMismatch)
            $maxSheetMismatchPct = [Math]::Max($maxSheetMismatchPct, $sheetMismatch)
            $maxLocalBalanceErrorPct = [Math]::Max($maxLocalBalanceErrorPct, $localBalance)
            $minimumScatteredPct = [Math]::Min($minimumScatteredPct, [Math]::Min($reflected, $forward))
        }
        catch {
            $failures.Add("${caseName}: invalid numeric CSV value: $($_.Exception.Message)")
        }
    }

    foreach ($angleGroup in ($rows | Group-Object ThetaDeg)) {
        $orderedRows = @($angleGroup.Group | Sort-Object { Parse-InvariantDouble $_.SkinDepth })
        for ($index = 1; $index -lt $orderedRows.Count; $index++) {
            $previous = $orderedRows[$index - 1]
            $current = $orderedRows[$index]
            $previousReflected = Parse-InvariantDouble $previous.ReflectedScatteredPct
            $currentReflected = Parse-InvariantDouble $current.ReflectedScatteredPct
            $previousForward = Parse-InvariantDouble $previous.ForwardScatteredPct
            $currentForward = Parse-InvariantDouble $current.ForwardScatteredPct
            if ($currentReflected -gt $previousReflected + 1e-8) {
                $failures.Add("theta=$($current.ThetaDeg): R_scat increases at skin=$($current.SkinDepth).")
            }
            if ($currentForward -gt $previousForward + 1e-8) {
                $failures.Add("theta=$($current.ThetaDeg): T_scat increases at skin=$($current.SkinDepth).")
            }
        }
    }

    [xml]$trx = Get-Content -LiteralPath $trxPath -Raw
    $counters = $trx.TestRun.ResultSummary.Counters
    $gitCommit = (& git rev-parse --short HEAD).Trim()
    $dotnetVersion = (& dotnet --version).Trim()
    $statusText = if ($failures.Count -eq 0) { 'ПРОЙДЕНО' } else { 'ОШИБКА' }

    $report = [System.Text.StringBuilder]::new()
    [void]$report.AppendLine('# Отчёт автоматической проверки')
    [void]$report.AppendLine()
    [void]$report.AppendLine("- Статус: **$statusText**")
    [void]$report.AppendLine("- Дата: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss K')")
    [void]$report.AppendLine("- Коммит: ``$gitCommit``")
    [void]$report.AppendLine("- Конфигурация: ``$Configuration``")
    [void]$report.AppendLine("- .NET SDK: ``$dotnetVersion``")
    [void]$report.AppendLine("- Автоматические тесты: пройдено $($counters.passed)/$($counters.total), ошибок $($counters.failed)")
    [void]$report.AppendLine("- Нативный CPU-модуль: $(if ($SkipNativeBuild) { 'сборка не запрашивалась' } else { 'успешно собран' })")
    [void]$report.AppendLine('- CUDA-модуль: проверены исходный код и интерфейс запуска; для исполнения нужен компьютер с CUDA')
    [void]$report.AppendLine()
    [void]$report.AppendLine('## Проверки поведения')
    [void]$report.AppendLine()
    [void]$report.AppendLine("- Число расчётных случаев: $($rows.Count) (5 углов x 6 толщин скин-слоя, N=30)")
    [void]$report.AppendLine("- Минимум R_scat/T_scat: $($minimumScatteredPct.ToString('F6', [System.Globalization.CultureInfo]::InvariantCulture))%")
    [void]$report.AppendLine("- Максимальное расхождение дальних полупространств: $($maxFarMismatchPct.ToString('E3', [System.Globalization.CultureInfo]::InvariantCulture))%")
    [void]$report.AppendLine("- Максимальное расхождение потоков сверху/снизу листа: $($maxSheetMismatchPct.ToString('E3', [System.Globalization.CultureInfo]::InvariantCulture))%")
    [void]$report.AppendLine("- Максимальная локальная невязка ЗСЭ: $($maxLocalBalanceErrorPct.ToString('F6', [System.Globalization.CultureInfo]::InvariantCulture))%")
    [void]$report.AppendLine('- Монотонность: R_scat и T_scat не должны возрастать с увеличением толщины скин-слоя при каждом проверенном угле')
    [void]$report.AppendLine()
    [void]$report.AppendLine('## Проверка отчётности')
    [void]$report.AppendLine()
    [void]$report.AppendLine('- Основные столбцы CSV: `ReflectedScatteredPct` и `ForwardScatteredPct`; обе величины неотрицательны.')
    [void]$report.AppendLine('- Локальная величина, которая может быть отрицательной, явно названа `OppositeSideSignedFluxPct`.')
    [void]$report.AppendLine('- Автоматически проверяются схема CSV, число строк, числовой формат, пороги предупреждений и формулировки отчёта.')
    [void]$report.AppendLine()
    [void]$report.AppendLine('## Результаты изменения толщины')
    [void]$report.AppendLine()
    [void]$report.AppendLine('| Угол, град. | Толщина скин-слоя | R_scat, % от I | T_scat, % от I | Расхождение, % | Невязка ЗСЭ, % |')
    [void]$report.AppendLine('|---:|---:|---:|---:|---:|---:|')
    foreach ($row in $rows) {
        [void]$report.AppendLine("| $($row.ThetaDeg) | $($row.SkinDepth) | $($row.ReflectedScatteredPct) | $($row.ForwardScatteredPct) | $($row.ScatteredHalfPlaneMismatchPct) | $($row.LocalBalanceErrorPct) |")
    }

    if ($failures.Count -gt 0) {
        [void]$report.AppendLine()
        [void]$report.AppendLine('## Обнаруженные ошибки')
        [void]$report.AppendLine()
        foreach ($failure in $failures) {
            [void]$report.AppendLine("- $failure")
        }
    }

    [System.IO.File]::WriteAllText(
        $reportPath,
        $report.ToString(),
        [System.Text.UTF8Encoding]::new($false))

    Write-Host "Отчёт проверки: $reportPath"
    Write-Host "Отчёт TRX: $trxPath"
    Write-Host "Таблица энергий: $sweepPath"

    if ($failures.Count -gt 0) {
        throw "Validation failed with $($failures.Count) issue(s). See $reportPath"
    }
}
finally {
    Pop-Location
}
