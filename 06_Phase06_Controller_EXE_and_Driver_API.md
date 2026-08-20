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


# Phase 06 — KernelMemoryLab.Controller.exe & Driver API

## Objective

完成第二個 EXE，透過標準 Windows Device I/O 介面使用 Driver。

## Important Clarification

`.exe` 不會像引用 DLL 一樣直接 reference `.sys`。

架構是：

```text
Controller.exe
   ↓ Open Device Handle
Win32 Device API
   ↓ DeviceIoControl
KernelMemoryLab.Driver.sys
```

Controller 內必須再包裝一層 `DriverClient` / `KernelMemoryApi`，UI 不直接組 raw IOCTL buffer。

## Public API

至少：

```text
Open()
Close()
IsOpen

GetProtocolVersion()
GetCapabilities()
Ping()

Read(pid, address, size)
Write(pid, address, byte[] data)

ReadBatch(pid, requests)
WriteBatch(pid, requests)
```

Typed Helpers：

```text
ReadInt32
ReadInt64
ReadFloat32

WriteInt32
WriteInt64
WriteFloat32
```

## UI

### Connection
顯示：
- Driver Connected / Disconnected
- Protocol Version
- Driver Version

### Target
顯示：
- Target Process Name
- PID
- Imported / Entered Address

### Single Read
欄位：
- PID
- Address
- Type / Size
- Read
- Result

### Single Write
欄位：
- PID
- Address
- Type
- Value
- Write
- Result

### Batch
表格：
- Name
- Address
- Type
- Read Value
- Write Value
- Status

## Address Input

支援：
- 手動貼上 Target UI address。
- Optional Target JSON Manifest Import。

不得加入：
- kernel address mode。
- physical address mode。
- arbitrary-process browser。

## Controller-side Validation

在送 IOCTL 前先拒絕：
- malformed hex。
- zero address。
- zero size。
- size above protocol limit。
- batch > limit。

Kernel Driver 仍必須再驗證，不能信任 Controller。

## Error UX

不能只顯示：

```text
Failed
```

至少呈現：
- operation；
- driver status；
- Win32 error（若有）；
- target PID；
- timestamp。

## Agent Verification

Agent 可：
- Build Controller。
- Unit test API serialization。
- Mock/Fake transport 測 UI/ViewModel。
- 驗證 driver unavailable 時 UI 不 crash。

Agent 不得：
- Open 真實 Driver。
- 發送真正 IOCTL。

## Manual VM Acceptance

由使用者完成：
- Connect。
- Ping。
- Single Read。
- Single Write。
- Batch Read。
- Batch Write。
