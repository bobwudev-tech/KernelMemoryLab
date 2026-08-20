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


# Phase 03 — KernelMemoryLab.Target.exe

## Objective

建立一個完全由本專案控制的 User-mode Target Process，提供穩定記憶體位址供後續 Driver Read/Write 驗證。

## Technology

- C# WPF
- x64
- 不依賴 Driver 即可執行

## Memory Model

不得使用一般可能被 GC relocation 的 managed field 作為測試地址。

Target 必須配置一塊 lifecycle-stable 的 unmanaged memory block。

該 block 從程式啟動後配置，直到程式結束才釋放。

## Required Variables

至少：

| Name | Type | Initial Value |
|---|---|---:|
| Health | Int32 | 100 |
| Mana | Int32 | 50 |
| Gold | Int64 | 1000 |
| PositionX | Float32 | 10.0 |
| PositionY | Float32 | 20.0 |

## UI

顯示：
- Process Name
- PID
- Test Block Base Address
- Variable Name
- Variable Type
- Variable Address (hex)
- Current Value
- Last Refresh Time

## Refresh Behavior

每 1 秒：

```text
Unmanaged Memory
      ↓ read
ViewModel/UI
      ↓
Screen
```

UI 不得只顯示 cached value。

因此若 Driver 在外部修改：

```text
Health = 100
→ Driver Write
Health = 777
```

下一次 UI Refresh 應顯示 777。

## Address Stability

同一個 process lifetime：
- Variable address 不得因 UI Refresh 改變。
- Address offset 必須固定。
- 重新啟動 Target 後 address 可以改變，這是正常 ASLR/allocator 行為。

## Optional Address Manifest

Target 可提供 Export：

```json
{
  "pid": 1234,
  "processName": "KernelMemoryLab.Target.exe",
  "variables": [
    {
      "name": "Health",
      "address": "0x...",
      "type": "Int32",
      "size": 4
    }
  ]
}
```

方便 Controller 測試，但不可將 address 視為跨 process restart 永久有效。

## Agent Acceptance Criteria

Agent 可直接驗證：
- Build。
- User-mode unit tests。
- unmanaged layout calculations。
- serialization。
- Target 自己讀寫自己的 test block。
- address 在同一 process lifecycle 內保持穩定。

不需要 Driver。
