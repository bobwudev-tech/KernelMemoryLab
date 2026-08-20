> [!CAUTION]
> # KERNEL DRIVER SAFETY RULE — MANDATORY
>
> 本專案包含 Windows Kernel-mode Driver。**Coding Agent 不得在任何電腦上執行會載入、安裝、啟動、停止、呼叫或驗證 `.sys` 的操作。**
>
> Agent 只允許：
> - 編輯/建立原始碼與文件
> - Restore / Build / Compile
> - Static Analysis / Lint
> - 不接觸 Driver 的純 User-mode Unit Test
> - 產生供使用者手動執行的 VM 測試步驟與命令
>
> Agent 禁止：
> - 安裝 / 移除 Driver
> - Load / Unload `.sys`
> - 建立或啟動 Kernel Driver Service
> - 對 Driver 執行 `DeviceIoControl`
> - Driver Verifier
> - BCDEdit / TESTSIGNING / Boot 設定修改
> - Kernel Debugger 操作
> - 任何可能造成 BSOD、Kernel state 改變或 Windows 啟動設定改變的命令
>
> **所有 Driver 實際驗證只能由使用者本人在 Windows 11 VM 手動執行。**
>
> 若文件內需要提供上述命令，必須清楚標註：
> `MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`
>
> 本專案 V1 僅允許對受控 User Process (`KernelMemoryLab.Target.exe`) 的 User Virtual Memory 進行讀寫。
> 嚴禁實作任意 Kernel VA、Physical Memory、CR3/Page Table、Kernel Patch、任意 Process、Security/Anti-cheat bypass 等能力。


# KernelMemoryLab — SDD Phase Package

## Goal

Kernel-mode Driver 對受控 User Process (`KernelMemoryLab.Target.exe`) 的 User Virtual Memory 執行：

```text
Single Read
Single Write
Batch Read
Batch Write
```

最終成品：

```text
KernelMemoryLab.Driver.sys
KernelMemoryLab.Target.exe
KernelMemoryLab.Controller.exe
```

## Development order

```text
00 Project Contract
01 Solution & Safety
02 Protocol & PING
03 Target.exe
04 Single R/W
05 Batch R/W
06 Controller.exe + API
07 Driver Package + Manual Install Guide
08 Manual VM Verification
09 Release + API Docs
```

## VM policy

Coding Agent 不操作 VM，也不執行任何 Driver Integration Test。

Driver 的安裝、載入、IOCTL 驗證、Verifier 等均由使用者本人在 Windows 11 VM 手動完成。

## Phase 01 build baseline

Phase 01 已建立：

- x64 KMDF Driver skeleton（無 device、IOCTL 或 memory R/W）。
- x64 C# WPF Target 與 Controller 空殼。
- Shared Protocol 專案位置（尚未定義完整 IOCTL）。
- 僅 Restore／Build 的 PowerShell pipeline。

需求：Visual Studio 的 C++ build tools、.NET Desktop build tools，以及相容的 Windows Driver Kit (WDK)。

```powershell
.\scripts\build.ps1 -Configuration Debug
.\scripts\build.ps1 -Configuration Release
```

`build.ps1` 只會 Restore 與 Build；不會安裝 INF、建立服務、載入 Driver 或呼叫 `DeviceIoControl`。

Phase 01 不要求任何手動 VM 測試。

## Phase 02 protocol baseline

Phase 02 固定 Protocol `1.0`，並實作 `GET_PROTOCOL_VERSION`、`GET_CAPABILITIES` 與 `PING`。Single／Batch R/W 只有保留 IOCTL code，尚未實作且不會出現在 capabilities 中。

Protocol wire layout、limits 與 IOCTL constants 記錄於 `docs/Protocol_V1.md`。純 User-mode protocol tests 位於 `tests/Unit/KernelMemoryLab.Protocol.Tests`。

Driver 的實際 PING 驗證只能由使用者依 `tests/ManualVm/Phase02_Ping_Checklist.md` 在 Windows 11 VM 手動執行。

## Phase 03 target baseline

Phase 03 建立獨立於 Driver 的 x64 WPF Target。程式啟動時配置一個 lifecycle-stable unmanaged memory block，包含 Health、Mana、Gold、PositionX 與 PositionY；UI 每秒直接從 unmanaged memory 重新讀取並顯示值與地址。

固定 layout 記錄於 `docs/Target_Memory_Layout.md`。純 User-mode 的 layout、自我讀寫、非快取刷新與地址穩定性測試位於 `tests/Unit/KernelMemoryLab.Target.Tests`。

## Phase 04 single read/write baseline

Phase 04 實作 `READ_SINGLE` 與 `WRITE_SINGLE`，僅允許 image basename 為 `KernelMemoryLab.Target.exe` 的受控 User Process。Driver 在存取前驗證 PID、process lifecycle、完整 user address range、4096-byte 上限、overflow 與 kernel-range boundary。

Protocol 與狀態定義記錄於 `docs/Protocol_V1.md`。純 User-mode encoder/decoder 與 range validation tests 位於 `tests/Unit/KernelMemoryLab.Protocol.Tests`。

真實 Driver single R/W 只能由使用者依 `tests/ManualVm/Phase04_Single_ReadWrite_Checklist.md` 在 Windows 11 VM 手動驗證。

## Phase 05 batch read/write baseline

Phase 05 實作 flat inline `READ_BATCH` 與 `WRITE_BATCH`，一個 request 只能使用一個受控 `KernelMemoryLab.Target.exe` PID。上限為 128 items、每項 4096 bytes、aggregate payload 524288 bytes；所有 offset arithmetic 均受檢查，並回傳 per-item result 以及明確的 overall success／partial／all-failed 狀態。

Wire format 與語意記錄於 `docs/Protocol_V1.md`，純 User-mode parser／serialization tests 位於 `tests/Unit/KernelMemoryLab.Protocol.Tests`。真實 Driver batch acceptance 只能由使用者依 `tests/ManualVm/Phase05_Batch_ReadWrite_Checklist.md` 在 Windows 11 VM 手動驗證。
