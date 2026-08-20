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


# Phase 05 — Batch User Process Memory Read / Write

## Objective

在單次 logical IOCTL 中處理多個非連續 User Process Memory 位置。

## Operations

- `READ_BATCH`
- `WRITE_BATCH`

## Batch Limits

V1 固定：
- 最大 128 items。
- 每 item 最大 4096 bytes。
- aggregate payload 有固定上限。
- 所有 offset / size 使用 checked arithmetic。
- 不接受 nested raw user pointer。

## Batch Read Item

每個 item 至少：
- Address
- Size
- Result Offset / Protocol-defined storage
- Per-item Status

## Batch Write Item

每個 item 至少：
- Address
- Size
- Data Offset
- Per-item Status

## Semantics

採用：

> **Per-item status + overall request status**

因此一個 item 無效時，不應造成 Driver crash。

規格需明確定義：
- 是否繼續後續 item（建議繼續）。
- overall status 如何表示 partial success。
- BytesProcessed 如何計算。

## Same Target Restriction

Batch 所有 item 只能指向同一個：
- PID
- `KernelMemoryLab.Target.exe`

不得在同一 batch 中跨 process。

## Validation

Batch request 先驗：
- item count。
- header。
- aggregate length。
- offsets。
- integer overflow。
- PID/process identity。

再逐 item 驗：
- address。
- size。
- user range。
- accessibility。

## Agent Verification

Agent 可：
- Build。
- Batch parser unit test。
- malformed offset unit test。
- overflow unit test。
- too-many-items unit test。
- request/response serialization test。

不得實際呼叫 Driver。

## Manual VM Acceptance

使用者驗證：
- 一次讀取 Health/Mana/Gold/X/Y。
- 一次寫入至少 3 個值。
- Target UI 更新。
- 中間插入一個 invalid item，Driver 不 crash。
- 過多 items 被拒絕。
- malformed payload 被拒絕。
