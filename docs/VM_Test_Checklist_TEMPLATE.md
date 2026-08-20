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


# KernelMemoryLab Manual VM Test Checklist — Template

> 所有項目由使用者本人在 Windows 11 VM 手動執行。

## Environment
- [ ] VM Snapshot 已建立
- [ ] Windows 11 版本已記錄
- [ ] x64
- [ ] Driver package version 已記錄
- [ ] Target version 已記錄
- [ ] Controller version 已記錄

## Driver
- [ ] Install
- [ ] Device/Service visible
- [ ] Controller can open driver
- [ ] PING
- [ ] Version / Capabilities

## Target
- [ ] PID shown
- [ ] addresses shown
- [ ] values refresh once per second

## Single
- [ ] Health read
- [ ] Health write
- [ ] Gold read/write
- [ ] Float read/write

## Batch
- [ ] Batch read
- [ ] Batch write

## Negative
- [ ] wrong PID denied
- [ ] non-target PID denied
- [ ] address 0 denied
- [ ] invalid user VA denied
- [ ] oversized request denied
- [ ] kernel-range address denied before access
- [ ] malformed batch denied

## Cleanup
- [ ] Controller closed
- [ ] Target closed
- [ ] Driver removed / stopped as documented
- [ ] Test result recorded
