param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

Write-Host "KernelMemoryLab build-only script."
Write-Host "This script MUST NOT install/load/start/call the kernel driver."

# The implementation agent should replace the following with the actual solution build command.
# Example:
# dotnet build ..\KernelMemoryLab.sln -c $Configuration
# or MSBuild for the WDK solution as appropriate.

Write-Host "Configuration: $Configuration"
