$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$buildDir = Join-Path $scriptDir 'build'
$sourceFile = Join-Path $scriptDir 'src\DiffractionCuda.cu'

New-Item -ItemType Directory -Force -Path $buildDir | Out-Null

$tempRoot = Join-Path $env:TEMP 'DiffractionCudaBuild'
$tempSrcDir = Join-Path $tempRoot 'src'
$tempBuildDir = Join-Path $tempRoot 'build'
New-Item -ItemType Directory -Force -Path $tempSrcDir | Out-Null
New-Item -ItemType Directory -Force -Path $tempBuildDir | Out-Null

$tempSourceFile = Join-Path $tempSrcDir 'DiffractionCuda.cu'
$tempOutputFile = Join-Path $tempBuildDir 'DiffractionCuda.exe'
Copy-Item -LiteralPath $sourceFile -Destination $tempSourceFile -Force

$vswhere = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path $vswhere)) {
    throw "Visual Studio locator not found: $vswhere"
}

$vsInstall = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if ([string]::IsNullOrWhiteSpace($vsInstall)) {
    throw 'Visual Studio C++ Build Tools not found.'
}

$vcvars = Join-Path $vsInstall 'VC\Auxiliary\Build\vcvars64.bat'
if (-not (Test-Path $vcvars)) {
    throw "vcvars64.bat not found: $vcvars"
}

$cudaArch = if ($args.Length -gt 0 -and -not [string]::IsNullOrWhiteSpace($args[0])) { $args[0] } else { '75' }
$outputFile = Join-Path $buildDir 'DiffractionCuda.exe'

$command = @"
call "$vcvars" && nvcc -allow-unsupported-compiler -std=c++17 -O3 -arch=sm_$cudaArch -Xcompiler="/utf-8 /EHsc /MD" "$tempSourceFile" -lcusolver -lcublas -o "$tempOutputFile"
"@

cmd.exe /c $command
if ($LASTEXITCODE -ne 0) {
    throw "nvcc build failed with exit code $LASTEXITCODE"
}

Copy-Item -LiteralPath $tempOutputFile -Destination $outputFile -Force
Write-Host "Built: $outputFile"
