$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$buildDir = Join-Path $scriptDir 'build'
$sourceFile = Join-Path $scriptDir 'src\DiffractionCpu.cpp'

New-Item -ItemType Directory -Force -Path $buildDir | Out-Null

$tempRoot = Join-Path $env:TEMP 'DiffractionCpuBuild'
$tempSrcDir = Join-Path $tempRoot 'src'
$tempBuildDir = Join-Path $tempRoot 'build'
New-Item -ItemType Directory -Force -Path $tempSrcDir | Out-Null
New-Item -ItemType Directory -Force -Path $tempBuildDir | Out-Null

$tempSourceFile = Join-Path $tempSrcDir 'DiffractionCpu.cpp'
$tempOutputFile = Join-Path $tempBuildDir 'DiffractionCpu.exe'
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

$command = @"
call "$vcvars" && cl /nologo /std:c++17 /O2 /utf-8 /EHsc /MD "$tempSourceFile" /Fe:"$tempOutputFile"
"@

Push-Location $tempBuildDir
try {
    cmd.exe /c $command
    if ($LASTEXITCODE -ne 0) {
        throw "cl build failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}

$outputFile = Join-Path $buildDir 'DiffractionCpu.exe'
Copy-Item -LiteralPath $tempOutputFile -Destination $outputFile -Force
Write-Host "Built: $outputFile"
