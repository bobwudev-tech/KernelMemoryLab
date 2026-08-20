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


# Phase 09 — API Guide, Release & Final Report

## Objective

整理最終可使用成果與完整教學。

## Final Functional Artifacts

```text
KernelMemoryLab.Driver.sys
KernelMemoryLab.Target.exe
KernelMemoryLab.Controller.exe
```

## Required Documentation

### 1. README.md
包含：
- 專案目的。
- 架構圖。
- User Process Memory scope。
- Safety limitation。
- Build 方法。
- 文件索引。

### 2. docs/Driver_Install.md
使用者在 Win11 VM 手動：
- install；
- verify；
- unload/stop；
- uninstall；
- troubleshooting。

### 3. docs/API_Usage.md

至少逐項說明：

```text
Open
Close
GetProtocolVersion
GetCapabilities
Ping

Read
Write
ReadBatch
WriteBatch

ReadInt32
ReadInt64
ReadFloat32
WriteInt32
WriteInt64
WriteFloat32
```

每個 API 必須包含：
- Purpose。
- Parameters。
- Return Type。
- Validation。
- Error。
- Minimal Usage Example。
- Thread-safety / lifetime note（如適用）。

### 4. docs/VM_Test_Checklist.md
Phase 08 的人工驗證流程。

### 5. docs/Verification_Report.md
由實際 VM Test 結果填寫。

## Release Package

```text
release/
├─ Driver/
│  ├─ KernelMemoryLab.Driver.sys
│  ├─ KernelMemoryLab.Driver.inf
│  └─ other required package files
├─ Apps/
│  ├─ KernelMemoryLab.Target.exe
│  └─ KernelMemoryLab.Controller.exe
├─ Docs/
│  ├─ Driver_Install.md
│  ├─ API_Usage.md
│  ├─ VM_Test_Checklist.md
│  └─ Verification_Report.md
└─ VERSION.txt
```

## Final Scope Assertion

Release Report 必須明確寫：

```text
KernelMemoryLab V1 implements kernel-mode access
to an explicitly allowed user-mode laboratory process.

It does NOT implement arbitrary kernel-memory access.
It does NOT implement physical-memory access.
It does NOT implement arbitrary-process access.
It does NOT implement anti-cheat/security bypass.
```

## Final Release Gate

必須同時滿足：
- Agent Build PASS。
- Agent Static/Unit Tests PASS。
- 使用者 VM Integration PASS。
- Driver Install Guide 已按 VM 實際流程修正。
- API Guide 與真正 binary/protocol version 相符。
