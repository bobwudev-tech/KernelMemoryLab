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


# Phase 04 — Single User Process Memory Read / Write

## Objective

讓 KernelMemoryLab.Driver 對 `KernelMemoryLab.Target.exe` 的 User Virtual Memory 執行單筆 Read / Write。

## Scope

實作：
- `READ_SINGLE`
- `WRITE_SINGLE`

不實作：
- Kernel memory。
- Physical memory。
- 任意 process。
- Security bypass。

## Request Model

### ReadSingle
Input：
- Protocol Header
- Target PID
- User Virtual Address
- Size

Output：
- Status
- BytesRead
- Data

### WriteSingle
Input：
- Protocol Header
- Target PID
- User Virtual Address
- Size
- Data

Output：
- Status
- BytesWritten

## Mandatory Driver Validation

進行實際 memory access 前必須全部通過：

1. PID 非 0。
2. PID 非 System。
3. Process 存在。
4. Process image identity 必須是 `KernelMemoryLab.Target.exe`。
5. Address != 0。
6. Size > 0。
7. Size <= 4096。
8. Address + Size 不得 integer overflow。
9. Address 必須落在合法 User Virtual Address Range。
10. 整個 request range 不得跨入 Kernel Address Range。
11. Request buffer 自身合法。
12. Process exit/race 必須安全處理。

## Failure Behavior

任何 Validation Failure：
- 不嘗試 memory access。
- 回傳明確 error/status。
- 不 bugcheck。
- 不 exception escape 到 kernel top level。
- 不留下半完成的 opaque state。

## Required Error Categories

至少：
- ProtocolMismatch
- InvalidRequest
- InvalidPid
- TargetNotFound
- TargetNotAllowed
- InvalidAddress
- InvalidSize
- AddressRangeOverflow
- KernelRangeDenied
- MemoryNotAccessible
- PartialTransfer
- TargetExited

## Agent Verification

Agent 只能：
- Compile。
- Static Analysis。
- Unit test request validation logic。
- Unit test overflow/range helper。
- Unit test protocol encoder/decoder。

Agent 不得執行真正 Driver Read/Write。

## Manual VM Test Matrix

使用者在 VM 手動測：

1. Read Health = 100。
2. Read Mana = 50。
3. Read Gold = 1000。
4. Write Health = 777。
5. 1 秒內 Target UI 顯示 777。
6. Write PositionX 新值。
7. Wrong PID → Denied。
8. Different process PID → Denied。
9. Address 0 → Denied。
10. Oversized Size → Denied。
11. Kernel-range address → Denied。
12. 關閉 Target 後再次 Read → graceful failure。
