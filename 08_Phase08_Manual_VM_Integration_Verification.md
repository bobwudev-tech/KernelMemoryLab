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


# Phase 08 — Manual Windows 11 VM Integration Verification

## Objective

此 Phase 完全由使用者本人執行。

Agent 的責任只有：
- 產生 checklist；
- 解讀使用者貼回的 log/error；
- 修改程式；
- rebuild；
- 更新 checklist。

Agent 不得直接操作 VM。

## Required Document

```text
docs/VM_Test_Checklist.md
```

## Pre-test

使用者自行：
- 建立 VM Snapshot。
- 確認測試 VM 可復原。
- 準備 Driver Package。
- 準備 Target.exe。
- 準備 Controller.exe。

## Test Order

### T01 — Driver Package
- 安裝 Driver。
- 確認裝置/服務狀態。
- Controller 可 Open Device。

### T02 — Protocol
- PING。
- Version。
- Capabilities。

### T03 — Target
- 啟動 Target.exe。
- 記錄 PID。
- 記錄 5 個 variable addresses。
- 確認 UI 1 sec refresh。

### T04 — Single Read
逐一讀：
- Health
- Mana
- Gold
- PositionX
- PositionY

比對 Target UI。

### T05 — Single Write
例如：
- Health 100 → 777
- Mana 50 → 123
- PositionX 10 → 25.5

確認 Target UI 在下一 refresh 顯示新值。

### T06 — Batch Read
一次讀取全部 test variables。

### T07 — Batch Write
一次更新多個 test variables。

### T08 — Negative Validation
只執行規格化、有限的錯誤案例：
- wrong PID。
- non-target process PID。
- PID 已退出。
- address 0。
- invalid user address。
- zero size。
- oversized size。
- too many batch items。
- malformed version。
- kernel-range address（預期 Driver 在 access 前拒絕）。

不得進行 unbounded fuzzing。

### T09 — Repeated Normal Operations
執行合理次數 read/write/batch，確認：
- 無 crash。
- 無明顯 leak。
- Target exit/restart 後 Controller 不使用舊 PID/address 繼續操作。

### T10 — Driver Verifier（Optional / Advanced）
若使用者決定執行：
- 僅在 VM。
- 僅針對本 Driver。
- 前置 Snapshot。
- 由使用者手動執行。

## Result Format

每項記錄：

```text
Test ID:
Build:
VM OS:
Driver Version:
Target Version:
Controller Version:
Result: PASS / FAIL
Observed:
Expected:
Error Code:
Notes:
```

## Failure Workflow

如果 BSOD：
1. 不立即重複同一測試。
2. 保存 dump。
3. 恢復 VM。
4. 把 dump analysis / bugcheck code / log 提供給 Agent。
5. Agent 只修改程式與分析，不操作 VM。
6. 使用者重新 Build package 後手動重測。
