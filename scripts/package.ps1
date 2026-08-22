param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

Write-Host "KernelMemoryLab package-only script."
Write-Host "This script builds and copies files only."
Write-Host "It MUST NOT install, load, start, stop, call, or verify a running Driver."
Write-Host "Configuration: $Configuration"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$buildScript = Join-Path $PSScriptRoot "build.ps1"
$buildRoot = Join-Path $repositoryRoot ("x64\" + $Configuration)
$driverStage = Join-Path $buildRoot "KernelMemoryLab.Driver"
$controllerOutput = Join-Path $repositoryRoot ("src\Controller\KernelMemoryLab.Controller\bin\x64\" + $Configuration + "\net8.0-windows")
$targetOutput = Join-Path $repositoryRoot ("src\Target\KernelMemoryLab.Target\bin\x64\" + $Configuration + "\net8.0-windows")
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$packageRoot = Join-Path $repositoryRoot ("artifacts\packages\KernelMemoryLab-Phase07-" + $Configuration + "-" + $timestamp)
$driverOutput = Join-Path $packageRoot "Driver"
$controllerPackage = Join-Path $packageRoot "Controller"
$targetPackage = Join-Path $packageRoot "Target"
$documentationOutput = Join-Path $packageRoot "Documentation"
$windowsKitsBin = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"

if (-not (Test-Path -LiteralPath $buildScript -PathType Leaf)) {
    throw "Build script not found: $buildScript"
}

if (Test-Path -LiteralPath $packageRoot) {
    throw "Refusing to overwrite an existing package directory: $packageRoot"
}

if (-not (Test-Path -LiteralPath $windowsKitsBin -PathType Container)) {
    throw "Windows Kits binary directory was not found: $windowsKitsBin"
}

$inf2CatPath = Get-ChildItem `
    -LiteralPath $windowsKitsBin `
    -Recurse `
    -Filter "Inf2Cat.exe" `
    -File |
    Where-Object { $_.Directory.Name -eq "x86" } |
    Sort-Object FullName -Descending |
    Select-Object -First 1 -ExpandProperty FullName

if ([string]::IsNullOrWhiteSpace($inf2CatPath)) {
    throw "Inf2Cat was not found. Install the Windows Driver Kit."
}

$rsa = $null
$certificate = $null
try {
    $rsa = [Security.Cryptography.RSA]::Create(3072)
    $request = [Security.Cryptography.X509Certificates.CertificateRequest]::new(
        "CN=KernelMemoryLab Phase07 Test",
        $rsa,
        [Security.Cryptography.HashAlgorithmName]::SHA256,
        [Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $keyUsage = [Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new(
        [Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature,
        $true)
    $request.CertificateExtensions.Add($keyUsage)
    $oids = [Security.Cryptography.OidCollection]::new()
    $null = $oids.Add([Security.Cryptography.Oid]::new("1.3.6.1.5.5.7.3.3"))
    $enhancedKeyUsage = [Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new(
        $oids,
        $false)
    $request.CertificateExtensions.Add($enhancedKeyUsage)
    $certificate = $request.CreateSelfSigned(
        [DateTimeOffset]::UtcNow.AddMinutes(-5),
        [DateTimeOffset]::UtcNow.AddYears(2))

    & $buildScript -Configuration $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE."
    }

    $builtDriver = Join-Path $buildRoot "KernelMemoryLab.Driver.sys"
    $driverSignature = Set-AuthenticodeSignature `
        -LiteralPath $builtDriver `
        -Certificate $certificate `
        -HashAlgorithm SHA256
    if ($null -eq $driverSignature.SignerCertificate) {
        throw "In-memory Driver test signing failed: $builtDriver"
    }

    # Refresh the already-created WDK staging directory with the signed SYS,
    # then regenerate the catalog directly over that single package directory
    # so Inf2Cat hashes that exact file. This does not install, load, or call
    # the Driver.
    Copy-Item `
        -LiteralPath $builtDriver `
        -Destination (Join-Path $driverStage "KernelMemoryLab.Driver.sys") `
        -Force
    & $inf2CatPath "/driver:$driverStage" /os:10_X64 /v
    if ($LASTEXITCODE -ne 0) {
        throw "Inf2Cat validation/catalog generation failed with exit code $LASTEXITCODE."
    }

$requiredDriverFiles = @(
    (Join-Path $driverStage "KernelMemoryLab.Driver.sys"),
    (Join-Path $driverStage "KernelMemoryLab.Driver.inf"),
    (Join-Path $driverStage "kernelmemorylab.driver.cat")
)

foreach ($file in $requiredDriverFiles) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        throw "Required Driver package file was not produced: $file"
    }
}

$catalogPath = Join-Path $driverStage "kernelmemorylab.driver.cat"
$catalogSignature = Set-AuthenticodeSignature `
    -LiteralPath $catalogPath `
    -Certificate $certificate `
    -HashAlgorithm SHA256
if ($null -eq $catalogSignature.SignerCertificate) {
    throw "In-memory catalog test signing failed: $catalogPath"
}

$debugPdb = Join-Path $buildRoot "KernelMemoryLab.Driver.pdb"
if (($Configuration -eq "Debug") -and
    (-not (Test-Path -LiteralPath $debugPdb -PathType Leaf))) {
    throw "Debug PDB was not produced: $debugPdb"
}

foreach ($directory in @(
    $driverOutput,
    $controllerPackage,
    $targetPackage,
    $documentationOutput)) {
    New-Item -ItemType Directory -Path $directory | Out-Null
}

Copy-Item -LiteralPath $requiredDriverFiles -Destination $driverOutput
[IO.File]::WriteAllBytes(
    (Join-Path $driverOutput "KernelMemoryLab.Driver.cer"),
    $certificate.Export(
        [Security.Cryptography.X509Certificates.X509ContentType]::Cert))
if ($Configuration -eq "Debug") {
    Copy-Item -LiteralPath $debugPdb -Destination $driverOutput
}

Copy-Item -Path (Join-Path $controllerOutput "*") -Destination $controllerPackage -Recurse
Copy-Item -Path (Join-Path $targetOutput "*") -Destination $targetPackage -Recurse
Copy-Item -LiteralPath (Join-Path $repositoryRoot "docs\Driver_Install.md") -Destination $documentationOutput
Copy-Item -LiteralPath (Join-Path $repositoryRoot "docs\API_Usage.md") -Destination $documentationOutput
Copy-Item -LiteralPath (Join-Path $repositoryRoot "tests\ManualVm\Phase06_Controller_API_Checklist.md") -Destination $documentationOutput

$infPath = Join-Path $driverOutput "KernelMemoryLab.Driver.inf"
$infText = Get-Content -LiteralPath $infPath -Raw
$requiredInfTokens = @(
    "[Version]",
    "CatalogFile = KernelMemoryLab.Driver.cat",
    "[DefaultInstall.NTamd64]",
    "[DefaultInstall.NTamd64.Services]",
    "ServiceType   = 1",
    "StartType     = 3",
    "ServiceBinary = %13%\KernelMemoryLab.Driver.sys",
    "[DefaultInstall.NTamd64.Wdf]",
    "KmdfLibraryVersion"
)

foreach ($token in $requiredInfTokens) {
    if ($infText.IndexOf($token, [StringComparison]::Ordinal) -lt 0) {
        throw "Static INF validation failed. Missing token: $token"
    }
}

foreach ($signedFile in @(
    (Join-Path $driverOutput "KernelMemoryLab.Driver.sys"),
    (Join-Path $driverOutput "kernelmemorylab.driver.cat"))) {
    $signature = Get-AuthenticodeSignature -LiteralPath $signedFile
    if (($signature.SignatureType -eq "None") -or ($null -eq $signature.SignerCertificate)) {
        throw "Static signature validation failed: $signedFile"
    }
}

$files = Get-ChildItem -LiteralPath $packageRoot -Recurse -File | Sort-Object FullName
$manifestFiles = foreach ($file in $files) {
    [ordered]@{
        Path = $file.FullName.Substring($packageRoot.Length + 1).Replace("\", "/")
        Size = $file.Length
        Sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    }
}

$manifest = [ordered]@{
    Package = "KernelMemoryLab Phase 07"
    Configuration = $Configuration
    Architecture = "x64"
    DriverVersion = "0.5.0.0"
    GeneratedUtc = (Get-Date).ToUniversalTime().ToString("O")
    DriverExecutionPerformed = $false
    Files = @($manifestFiles)
}

$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $packageRoot "PackageManifest.json") -Encoding utf8

Write-Host "Package created: $packageRoot"
Write-Host "Static INF/signature checks passed."
Write-Host "No Driver installation, service control, device access, IOCTL, boot change, or reboot was performed."
}
finally {
    if ($null -ne $certificate) {
        $certificate.Dispose()
    }
    if ($null -ne $rsa) {
        $rsa.Dispose()
    }
}
