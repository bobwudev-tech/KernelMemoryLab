param(
    [string]$SourcePackage,

    [ValidateSet("ReleaseCandidate", "Final")]
    [string]$ReleaseStatus = "ReleaseCandidate"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

Write-Host "KernelMemoryLab release files-only script."
Write-Host "This script MUST NOT install, load, start, stop, call, or verify a running Driver."

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$packagesRoot = Join-Path $repositoryRoot "artifacts\packages"
$releasesRoot = Join-Path $repositoryRoot "artifacts\releases"
$verificationReport = Join-Path $repositoryRoot "docs\Verification_Report.md"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$version = "0.5.0.0"
$channel = if ($ReleaseStatus -eq "Final") { "final" } else { "rc1" }

if ([string]::IsNullOrWhiteSpace($SourcePackage)) {
    $SourcePackage = Get-ChildItem -LiteralPath $packagesRoot -Directory |
        Where-Object Name -Like "KernelMemoryLab-Phase07-Release-*" |
        Sort-Object Name -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}

if ([string]::IsNullOrWhiteSpace($SourcePackage) -or
    (-not (Test-Path -LiteralPath $SourcePackage -PathType Container))) {
    throw "A Phase 07 Release package was not found."
}

$sourceManifestPath = Join-Path $SourcePackage "PackageManifest.json"
if (-not (Test-Path -LiteralPath $sourceManifestPath -PathType Leaf)) {
    throw "Source PackageManifest.json was not found: $sourceManifestPath"
}

$sourceManifest = Get-Content -LiteralPath $sourceManifestPath -Raw | ConvertFrom-Json
if ($sourceManifest.Configuration -ne "Release" -or
    $sourceManifest.Architecture -ne "x64" -or
    $sourceManifest.DriverVersion -ne $version -or
    $sourceManifest.DriverExecutionPerformed -ne $false) {
    throw "Source package identity or safety metadata does not match the V1 Release requirements."
}

foreach ($entry in $sourceManifest.Files) {
    $sourceFile = Join-Path $SourcePackage $entry.Path
    if (-not (Test-Path -LiteralPath $sourceFile -PathType Leaf)) {
        throw "Source manifest file is missing: $($entry.Path)"
    }

    $actualHash = (Get-FileHash -LiteralPath $sourceFile -Algorithm SHA256).Hash
    if ($actualHash -ne $entry.Sha256) {
        throw "Source manifest hash mismatch: $($entry.Path)"
    }
}

$reportText = Get-Content -LiteralPath $verificationReport -Raw
if ($ReleaseStatus -eq "Final") {
    $phase08Passed = $reportText -match "(?m)^Overall Phase 08 Result: PASS\s*$"
    $finalGatePassed = $reportText -match "(?m)^Final Release Gate: PASS\s*$"
    $pendingGateExists = $reportText -match "(?m)^(Overall Phase 08 Result|Final Release Gate): PENDING"
    if ((-not $phase08Passed) -or (-not $finalGatePassed) -or $pendingGateExists) {
        throw "Final release refused: manual VM integration and final gate are not PASS."
    }
}

$packageRoot = Join-Path $releasesRoot ("KernelMemoryLab-V1-" + $version + "-" + $channel + "-" + $timestamp)
$releaseRoot = Join-Path $packageRoot "release"
$driverRoot = Join-Path $releaseRoot "Driver"
$appsRoot = Join-Path $releaseRoot "Apps"
$docsRoot = Join-Path $releaseRoot "Docs"

if (Test-Path -LiteralPath $packageRoot) {
    throw "Refusing to overwrite an existing release directory: $packageRoot"
}

foreach ($directory in @($driverRoot, $appsRoot, $docsRoot)) {
    New-Item -ItemType Directory -Path $directory | Out-Null
}

$sourceDriver = Join-Path $SourcePackage "Driver"
$sourceController = Join-Path $SourcePackage "Controller"
$sourceTarget = Join-Path $SourcePackage "Target"
foreach ($directory in @($sourceDriver, $sourceController, $sourceTarget)) {
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
        throw "Required source package directory is missing: $directory"
    }
}

Copy-Item -Path (Join-Path $sourceDriver "*") -Destination $driverRoot -Recurse
Copy-Item -Path (Join-Path $sourceTarget "*") -Destination $appsRoot -Recurse

# Target and Controller share the same Protocol assembly. Refuse a conflicting
# duplicate instead of silently mixing different builds.
foreach ($file in Get-ChildItem -LiteralPath $sourceController -File) {
    $destination = Join-Path $appsRoot $file.Name
    if (Test-Path -LiteralPath $destination -PathType Leaf) {
        $sourceHash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        $destinationHash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
        if ($sourceHash -ne $destinationHash) {
            throw "Controller/Target dependency conflict: $($file.Name)"
        }
    }
    else {
        Copy-Item -LiteralPath $file.FullName -Destination $destination
    }
}

$requiredDocs = @(
    "Driver_Install.md",
    "API_Usage.md",
    "VM_Test_Checklist.md",
    "Verification_Report.md"
)
foreach ($document in $requiredDocs) {
    $source = Join-Path $repositoryRoot ("docs\" + $document)
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        throw "Required release document is missing: $source"
    }
    Copy-Item -LiteralPath $source -Destination $docsRoot
}
Copy-Item -LiteralPath (Join-Path $repositoryRoot "docs\Protocol_V1.md") -Destination $docsRoot
Copy-Item -LiteralPath (Join-Path $repositoryRoot "README.md") -Destination $docsRoot

$requiredArtifacts = @(
    (Join-Path $driverRoot "KernelMemoryLab.Driver.sys"),
    (Join-Path $driverRoot "KernelMemoryLab.Driver.inf"),
    (Join-Path $driverRoot "kernelmemorylab.driver.cat"),
    (Join-Path $appsRoot "KernelMemoryLab.Target.exe"),
    (Join-Path $appsRoot "KernelMemoryLab.Controller.exe")
)
foreach ($file in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        throw "Required release artifact is missing: $file"
    }
}

$infText = Get-Content -LiteralPath (Join-Path $driverRoot "KernelMemoryLab.Driver.inf") -Raw
if ($infText -notmatch "DriverVer\s*=\s*08/22/2026,0\.5\.0\.0") {
    throw "Driver INF version does not match $version."
}

foreach ($signedFile in @(
    (Join-Path $driverRoot "KernelMemoryLab.Driver.sys"),
    (Join-Path $driverRoot "kernelmemorylab.driver.cat"))) {
    $signature = Get-AuthenticodeSignature -LiteralPath $signedFile
    if (($signature.SignatureType -eq "None") -or ($null -eq $signature.SignerCertificate)) {
        throw "Static signature presence check failed: $signedFile"
    }
}

$privateFiles = @(Get-ChildItem -LiteralPath $releaseRoot -Recurse -File |
    Where-Object Extension -In ".pfx", ".pvk", ".key", ".pem")
if ($privateFiles.Count -ne 0) {
    throw "Private signing material must not be included in a release."
}

$gate = if ($ReleaseStatus -eq "Final") { "PASS" } else { "PENDING_MANUAL_VM_INTEGRATION" }
$versionLines = @(
    "Product=KernelMemoryLab V1",
    "ProductVersion=$version",
    "ProtocolVersion=1.0",
    "Architecture=x64",
    "ReleaseChannel=$channel",
    "FinalReleaseGate=$gate",
    "SourcePackageManifestSha256=$((Get-FileHash -LiteralPath $sourceManifestPath -Algorithm SHA256).Hash)",
    "GeneratedUtc=$((Get-Date).ToUniversalTime().ToString('O'))",
    "DriverExecutionPerformed=false"
)
$versionLines | Set-Content -LiteralPath (Join-Path $releaseRoot "VERSION.txt") -Encoding utf8

$files = Get-ChildItem -LiteralPath $releaseRoot -Recurse -File | Sort-Object FullName
$manifestFiles = foreach ($file in $files) {
    [ordered]@{
        Path = $file.FullName.Substring($releaseRoot.Length + 1).Replace("\", "/")
        Size = $file.Length
        Sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    }
}

$releaseManifest = [ordered]@{
    Product = "KernelMemoryLab V1"
    ProductVersion = $version
    ProtocolVersion = "1.0"
    Architecture = "x64"
    ReleaseChannel = $channel
    FinalReleaseGate = $gate
    GeneratedUtc = (Get-Date).ToUniversalTime().ToString("O")
    SourcePackageManifestSha256 = (Get-FileHash -LiteralPath $sourceManifestPath -Algorithm SHA256).Hash
    DriverExecutionPerformed = $false
    Files = @($manifestFiles)
}
$releaseManifest | ConvertTo-Json -Depth 5 |
    Set-Content -LiteralPath (Join-Path $releaseRoot "ReleaseManifest.json") -Encoding utf8

Write-Host "Release created: $releaseRoot"
Write-Host "Final release gate: $gate"
Write-Host "No Driver installation, service control, device access, IOCTL, boot change, or reboot was performed."
