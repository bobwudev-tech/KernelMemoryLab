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
