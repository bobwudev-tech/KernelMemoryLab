param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

Write-Host "KernelMemoryLab build-only script."
Write-Host "This script MUST NOT install/load/start/call the kernel driver."
Write-Host "Configuration: $Configuration"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repositoryRoot "KernelMemoryLab.sln"
$vsWherePath = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"

if (-not (Test-Path -LiteralPath $solutionPath -PathType Leaf)) {
    throw "Solution not found: $solutionPath"
}

if (-not (Test-Path -LiteralPath $vsWherePath -PathType Leaf)) {
    throw "Visual Studio Installer discovery tool was not found: $vsWherePath"
}

$msBuildPath = & $vsWherePath `
    -latest `
    -products * `
    -requires Microsoft.Component.MSBuild `
    -find "MSBuild\**\Bin\MSBuild.exe" |
    Select-Object -First 1

if ([string]::IsNullOrWhiteSpace($msBuildPath)) {
    throw "MSBuild was not found. Install Visual Studio with C++ and WDK build support."
}

Write-Host "MSBuild: $msBuildPath"

# Build only. Do not add deployment, installation, service-control, IOCTL, or
# driver execution commands to this script.
& $msBuildPath $solutionPath `
    /restore `
    /m `
    /nologo `
    /verbosity:minimal `
    /p:Configuration=$Configuration `
    /p:Platform=x64

if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}

Write-Host "Build completed. No driver deployment or execution was performed."
