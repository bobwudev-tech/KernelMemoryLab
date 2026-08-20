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


# Phase 02 — Driver Protocol & PING

## Objective

在實作 Memory R/W 前，先固定 User↔Kernel Protocol。

## Required Operations

Phase 02 實作：
- `GET_PROTOCOL_VERSION`
- `GET_CAPABILITIES`
- `PING`

預留但此 Phase 不實作：
- `READ_SINGLE`
- `WRITE_SINGLE`
- `READ_BATCH`
- `WRITE_BATCH`

## Protocol Version

```text
Major = 1
Minor = 0
```

每個 request / response 必須可辨識版本。

## Common Request Header

至少包含：
- ProtocolVersion
- StructureSize
- Flags
- Reserved

Reserved 必須為 0。

## Common Response

至少包含：
- ProtocolVersion
- OperationStatus
- BytesProcessed
- Optional detail status

## V1 Limits

規格先固定：
- Single item 最大 4096 bytes。
- Batch 最大 128 items。
- Batch aggregate payload 必須有最大值。
- Address 使用 UInt64。
- Size 使用固定寬度 unsigned integer。
- 所有 size/offset calculation 必須 checked。

## IOCTL Buffer Rules

不得在 protocol 中接受：
- arbitrary nested user pointers；
- raw function pointers；
- kernel addresses；
- physical addresses。

Request buffer 必須使用可被 Driver 安全驗證的扁平資料結構。

## PING Behavior

Controller 提供：
- protocol version；
- known token / nonce。

Driver 回傳：
- protocol version；
- driver build/version；
- capabilities；
- echo token。

## Agent-Allowed Verification

可做：
- struct size unit test；
- serialization test；
- IOCTL code constant test；
- compile；
- static analysis。

不可做：
- Open driver；
- DeviceIoControl；
- driver load。

## Manual VM Acceptance Criteria

由使用者手動確認：
- Driver 可載入。
- PING 成功。
- Version mismatch 被拒絕。
- malformed structure 被拒絕。
- 不造成 BSOD。
