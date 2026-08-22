> [!CAUTION]
> `MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`
>
> 本文件中的 Driver、certificate、service、boot 與 device-open 操作，只能由使用者本人在可還原的 Windows 11 x64 VM 手動執行。不要在 host 或重要電腦上執行。

# KernelMemoryLab Driver Package — Windows 11 VM Manual Install Guide

## 1. Scope and package model

KernelMemoryLab is a non-PnP KMDF control-device Driver. Its INF is a Windows 11 x64 primitive-driver package: it has an architecture-decorated `DefaultInstall.NTamd64`, no `Manufacturer` section, uses Driver Store directory ID 13, and creates a demand-start kernel service. This follows Microsoft's [primitive driver requirements](https://learn.microsoft.com/en-us/windows-hardware/drivers/develop/creating-a-primitive-driver).

This package is only for the controlled `KernelMemoryLab.Target.exe` user virtual-memory lab. It is not production-signed and must never be deployed to a physical workstation or production VM.

## 2. Package contents

Run the build/package commands on the development host. They only compile, validate, and copy files:

```powershell
.\scripts\package.ps1 -Configuration Release
.\scripts\package.ps1 -Configuration Debug
```

Each invocation creates a new timestamped directory under `artifacts\packages` and refuses to overwrite an existing directory.

| Path | Purpose |
|---|---|
| `Driver\KernelMemoryLab.Driver.sys` | x64 KMDF Driver image |
| `Driver\KernelMemoryLab.Driver.inf` | primitive-driver install manifest |
| `Driver\KernelMemoryLab.Driver.cat` | signed package catalog |
| `Driver\KernelMemoryLab.Driver.cer` | public test certificate only; no private key |
| `Driver\KernelMemoryLab.Driver.pdb` | Debug package only |
| `Controller\` | framework-dependent .NET 8 WPF Controller output |
| `Target\` | framework-dependent .NET 8 WPF Target output |
| `Documentation\` | install/API/manual acceptance guides |
| `PackageManifest.json` | configuration, architecture, hashes, sizes, generation time |

Never copy a `.pfx`, `.pvk`, private key, or development certificate store into the VM package.

## 3. VM prerequisites

- Windows 11 x64 VM, fully isolated from production workloads.
- Administrator access inside the VM.
- A VM snapshot/checkpoint taken while the VM is powered off or in a known clean state.
- Release package copied intact to `C:\KernelMemoryLab\Phase07`.
- [.NET 8 Desktop Runtime x64](https://dotnet.microsoft.com/download/dotnet/8.0) for the framework-dependent Controller and Target.
- No Visual C++ Redistributable is required by the Driver package. Building from source requires Visual Studio C++ tools plus WDK, but the VM installation does not.
- Secure Boot/test-signing policy appropriate for an isolated test VM. Microsoft documents that loading test-signed kernel code requires TESTSIGNING and that Secure Boot can prevent changing that option: [Loading Test Signed Code](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/the-testsigning-boot-configuration-option).

## 4. Confirm the snapshot before changing the VM

In Hyper-V, VMware, VirtualBox, or the selected hypervisor UI:

1. Shut down or checkpoint the VM in a consistent state.
2. Create a snapshot named similar to `Before-KernelMemoryLab-Phase07`.
3. Record snapshot name, creation time, Windows build (`winver`), and package manifest SHA-256 values in the test report.
4. Verify that the hypervisor displays the snapshot and offers a restore/revert action.

Do not continue if the snapshot cannot be confirmed.

## 5. Inspect the copied package before installation

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
$package = "C:\KernelMemoryLab\Phase07"
Get-Content -LiteralPath "$package\PackageManifest.json"
Get-FileHash -Algorithm SHA256 -LiteralPath "$package\Driver\KernelMemoryLab.Driver.sys"
Get-AuthenticodeSignature -LiteralPath "$package\Driver\KernelMemoryLab.Driver.sys" | Format-List Status,StatusMessage,SignerCertificate
Get-AuthenticodeSignature -LiteralPath "$package\Driver\KernelMemoryLab.Driver.cat" | Format-List Status,StatusMessage,SignerCertificate
```

- Compare the SYS hash with `PackageManifest.json`.
- Before certificate import, signature status may report an untrusted root; the signer must still be present and must match the packaged public certificate.
- Reject the package if hashes differ, files are missing, or the signer is unexpected.

## 6. Manually prepare the isolated test environment

If BitLocker protects the VM boot volume, suspend protection through the Windows BitLocker control panel before changing Secure Boot/BCD, and record the recovery key outside the VM. Power off the VM and disable Secure Boot in the VM firmware settings only when required for this deliberately test-signed package.

Enable test-signing from an elevated terminal, then restart. Microsoft warns that BCDEdit changes can make a system unbootable, which is why the snapshot is mandatory.

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
bcdedit.exe /set TESTSIGNING ON
shutdown.exe /r /t 0
```

After restart, confirm the `Test Mode` desktop watermark. Do not use `nointegritychecks`, kernel debugging, or any security-bypass alternative.

## 7. Trust the packaged public test certificate

Microsoft requires a test certificate to be placed in the local-machine Trusted Root Certification Authorities and Trusted Publishers stores for package verification; see [Installing a Test Certificate](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/installing-a-test-certificate-on-a-test-computer).

From an elevated terminal:

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
$certificate = "C:\KernelMemoryLab\Phase07\Driver\KernelMemoryLab.Driver.cer"
certutil.exe -addstore -f Root $certificate
certutil.exe -addstore -f TrustedPublisher $certificate
certutil.exe -dump $certificate
```

Record the certificate subject, SHA-1 thumbprint, and expiration. This public `.cer` contains no private key.

## 8. Install the primitive Driver package

The architecture-decorated DefaultInstall entry is passed through Windows SetupAPI. On Windows 10 1903 and later, SetupAPI recognizes a compliant primitive INF and uses the managed primitive-driver installation path.

Run from an elevated terminal:

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
$inf = "C:\KernelMemoryLab\Phase07\Driver\KernelMemoryLab.Driver.inf"
rundll32.exe setupapi.dll,InstallHinfSection DefaultInstall 132 $inf
```

If Windows requests a restart, allow it manually after saving the test state. Do not continue after any signature or catalog warning until it has been investigated.

## 9. Confirm package and service existence

Windows assigns an `oem#.inf` Published Name. Record the exact value; it is required for removal. `PnPUtil` is included with Windows and manages Driver Store packages: [PnPUtil command syntax](https://learn.microsoft.com/en-us/windows-hardware/drivers/devtest/pnputil-command-syntax).

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
pnputil.exe /enum-drivers /class System /files
sc.exe query KernelMemoryLab
sc.exe qc KernelMemoryLab
```

Expected service configuration:

- Service name `KernelMemoryLab`.
- Type `KERNEL_DRIVER`.
- Start type `DEMAND_START`.
- Binary installed from the Driver Store package.

If no matching provider/package appears, inspect the logs in section 13 before retrying.

## 10. Start and confirm that the device can be opened

Start the service only after package/signature checks pass:

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
sc.exe start KernelMemoryLab
sc.exe query KernelMemoryLab
```

Expected state is `RUNNING`. The Driver creates `\\.\KernelMemoryLab` and grants access only to SYSTEM and administrators.

Device-open and protocol checks invoke the real Driver. Launch the Controller from the package and click `Connect`; it should show protocol `1.0`, Driver `0.5.0.0`, and capabilities `0x0000000000000F07`.

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
& "C:\KernelMemoryLab\Phase07\Controller\KernelMemoryLab.Controller.exe"
```

Continue with the packaged `Phase06_Controller_API_Checklist.md`. A successful service query alone does not prove that the device or IOCTL protocol works.

## 11. Stop and remove completely

Close Controller and Target first. Replace `oem42.inf` below with the exact Published Name recorded during installation. Never guess the `oem#.inf` value.

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
sc.exe stop KernelMemoryLab
pnputil.exe /delete-driver oem42.inf /uninstall
pnputil.exe /enum-drivers /class System /files
sc.exe query KernelMemoryLab
```

Expected results:

- The matching KernelMemoryLab Driver Store package no longer appears.
- `sc query` returns service-not-found (`1060`) after primitive-package removal.
- If removal reports that files are in use, close Controller/Target, confirm the service is stopped, then retry. Do not use `/force` until the VM snapshot and exact Published Name have been reconfirmed.

Remove the packaged test certificate through `certlm.msc` from both Local Computer stores—Trusted Root Certification Authorities and Trusted Publishers—using the exact subject/thumbprint recorded in section 7. Do not remove certificates by a partial name.

To leave test mode, restore Secure Boot/BitLocker settings only after the package and certificate have been removed:

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
bcdedit.exe /set TESTSIGNING OFF
shutdown.exe /r /t 0
```

After restart, confirm the Test Mode watermark is gone.

## 12. Restore the VM

For the cleanest recovery, power off the VM and revert to the snapshot recorded in section 4. Confirm the snapshot operation succeeded, then verify:

- KernelMemoryLab service and Driver Store package are absent.
- Test Mode watermark is absent.
- Test certificate is absent from both local-machine stores.
- Secure Boot and BitLocker settings match the pre-test baseline.
- `C:\KernelMemoryLab\Phase07` is absent unless intentionally retained outside the snapshot.

## 13. Troubleshooting and diagnostics

### Common errors

| Symptom/code | Likely cause | Action |
|---|---|---|
| `577` / Windows cannot verify the digital signature | Test certificate not trusted, TESTSIGNING off, catalog mismatch, or Secure Boot policy | Stop; verify hashes/signatures, certificate stores, Test Mode, and Code Integrity events. Do not bypass integrity checks. |
| BCDEdit reports Secure Boot policy protection | Secure Boot still enabled | Stop, return to the hypervisor firmware settings, and confirm the snapshot/BitLocker plan. |
| `Access is denied` / error 5 | Terminal or Controller is not elevated; device ACL permits administrators/SYSTEM only | Relaunch in the VM as administrator. |
| `The system cannot find the file specified` / error 2 | Missing SYS/catalog, incorrect package path, or failed Driver Store copy | Recheck package hashes and SetupAPI log. |
| Service error `1060` | Package/service was not installed or was already removed | Check Published Name and SetupAPI log; do not create an ad-hoc service. |
| Service error `1073` | A previous install remains | Stop and identify/remove the exact existing package before retrying. |
| Controller reports Win32 error `2` or `1060` | Device symbolic link is absent because Driver is stopped/not installed | Check `sc query`, signing events, and installation logs. |
| Catalog/hash failure | A package file changed after catalog generation | Discard the copied package and recopy a freshly generated package intact. |

### SetupAPI logs

Microsoft records Driver installation activity in `%SystemRoot%\INF\setupapi.dev.log`; `!!!` lines indicate failures. See [SetupAPI device installation logs](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/setupapi-device-installation-log-entries).

Read-only inspection:

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
Select-String -Path "$env:SystemRoot\INF\setupapi.dev.log" -Pattern "KernelMemoryLab","!!!" -Context 3,8
```

### Event Viewer and Code Integrity

Open Event Viewer and inspect:

- `Windows Logs > System` — Service Control Manager events.
- `Applications and Services Logs > Microsoft > Windows > CodeIntegrity > Operational` — signature/image verification failures, as documented in [Code Integrity event messages](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/code-integrity-event-log-messages).
- Filter by the installation/start timestamp recorded in the checklist.

### Debug output

The current Phase 07 Driver does not emit custom `DbgPrint`/ETW trace messages. Do not attach a kernel debugger merely to validate installation. Use SetupAPI, Code Integrity, System events, Controller structured errors, and the Debug PDB for offline dump symbolization if the user later performs a separately authorized VM diagnostic workflow.

## 14. Completion record

Record the following before declaring manual acceptance:

- Windows 11 edition/build and VM snapshot name.
- Package manifest path and SYS/CAT hashes.
- Certificate subject/thumbprint.
- Published Name (`oem#.inf`).
- Install/start/device-open results with timestamps.
- Stop/uninstall/test-mode restoration results.
- Relevant SetupAPI/Event Viewer entries and any unexpected VM behavior.
