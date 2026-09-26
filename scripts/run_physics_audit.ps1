param(
    [string]$OutputCsv = '',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $OutputCsv) { $OutputCsv = Join-Path $root 'artifacts/physics-audit/current-results.csv' }
if (-not $SkipBuild) {
    & dotnet build (Join-Path $root 'Diffraction.Tests/Diffraction.Tests.csproj') -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Managed build failed.' }
}
$assemblies = Join-Path $root 'Diffraction.Tests/bin/Release/net472'
Add-Type -Path (Join-Path $assemblies 'MathNet.Numerics.dll')
Add-Type -Path (Join-Path $assemblies 'Diffraction.Core.dll')
$originalCulture = [Threading.Thread]::CurrentThread.CurrentCulture
try {
    [Threading.Thread]::CurrentThread.CurrentCulture = [Globalization.CultureInfo]::InvariantCulture
    $rows = foreach ($skin in 0.0, 0.01) {
        $orders = if ($skin -eq 0) { @(55) } else { @(10, 30, 55, 80) }
        foreach ($n in $orders) {
            foreach ($degrees in 10, 45, 90) {
                foreach ($method in 'collocation', 'galerkin') {
                    $theta = $degrees * [Math]::PI / 180
                    if ($method -eq 'galerkin') {
                        $solver = [Diffraction.Core.GalerkinSolver]::SolveSinglePlate(
                            -1.5, -0.5, 1.0, $theta, $n, $skin, [Threading.CancellationToken]::None)
                    } else {
                        $solver = [Diffraction.Core.DiffractionMath+DifrOnLenta]::new(-1.5, -0.5, 1.0, $theta, $n, $skin)
                        if ($solver.SolveDifr([Threading.CancellationToken]::None) -ne 1) { throw 'Solve failed.' }
                    }
                    $far = $solver.CalculateFarFieldScatteredEnergy(720, 400)
                    $ext = $solver.CalculateExtinctionEnergy(400)
                    $abs = $solver.CalculateAbsorbedEnergy()
                    $local = $solver.CalculatePlateFluxComponents(400)
                    $incident = $solver.CalculatePlateIncidentEnergy()
                    $edge = $null
                    if ($skin -gt 0) {
                        $edge = 0.0
                        foreach ($fraction in 0.0, 1e-8, 1e-6, 1e-4, 1e-2) {
                            foreach ($x in (-1.5 + $fraction), (-0.5 - $fraction)) {
                                $residual = $solver.u_on_strip($x) - $solver.SheetCoefficient * $solver.CurrentDensity($x)
                                $edge = [Math]::Max($edge, [Diffraction.Core.DiffractionMath+Compl]::Abs($residual))
                            }
                        }
                    }
                    [pscustomobject]@{
                        method = $method; n = $n; wavelength_um = 1.0; plate_start_um = -1.5; plate_end_um = -0.5
                        angle_deg = $degrees; skin_depth_um = $skin
                        c_back_um = $far.ReflectedScattered / [Math]::PI
                        c_forward_um = $far.TransmittedScattered / [Math]::PI
                        c_abs_um = $abs / [Math]::PI; c_ext_um = $ext / [Math]::PI
                        back_pct_geometric_reference = 100 * $far.ReflectedScattered / $incident
                        optical_error_pct_extinction = 100 * [Math]::Abs($ext - $far.TotalScattered - $abs) / $ext
                        local_error_pct_geometric_reference = 100 * [Math]::Abs($local.AbsorbedFromFlux - $abs) / $incident
                        mean_boundary_residual = $solver.VerifyBoundaryConditions()
                        max_edge_boundary_residual = $edge
                    }
                }
            }
        }
    }
    $directory = Split-Path -Parent ([IO.Path]::GetFullPath($OutputCsv))
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $rows | Export-Csv -LiteralPath $OutputCsv -NoTypeInformation -Encoding UTF8
    Write-Output "Saved $($rows.Count) independent calculations: $OutputCsv"
} finally {
    [Threading.Thread]::CurrentThread.CurrentCulture = $originalCulture
}
