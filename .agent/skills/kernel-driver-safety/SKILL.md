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


# Skill — Kernel Driver Safety

## Trigger

只要任務涉及：
- `.sys`
- INF driver install
- Kernel service
- DeviceIoControl integration
- Test Signing
- BCDEdit
- Driver Verifier
- Kernel Debugging

就必須先套用此 Skill。

## Decision

### Safe for Agent
可直接執行：
- source edit
- compile
- static analysis
- unit tests that do not communicate with a real driver
- mock transport
- docs

### Manual VM Only
只能產生步驟，禁止 Agent 執行：
- install/remove driver
- load/unload driver
- start/stop kernel service
- open real driver handle
- call real IOCTL
- Driver Verifier
- BCDEdit/testsigning
- reboot for driver work
- kernel debugger

## Required Phrase

在所有需要使用者手動執行的 kernel-related command 之前放：

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

## Scope Guard

若 implementation request 會擴張至：
- arbitrary kernel memory
- physical memory
- arbitrary process
- bypass/security evasion

停止擴張，維持 V1 user-process laboratory scope。
