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


# Phase 07 — Driver Package & Manual VM Install Guide

## Objective

建立可供使用者在 Windows 11 VM 手動安裝的 Driver Package，並提供完整文件。

## Deliverables

依 Driver Model 產生所需：
- `KernelMemoryLab.Driver.sys`
- `.inf`
- `.cat` / test signing artifacts（如適用）
- PDB（Debug package）
- `docs/Driver_Install.md`

## Driver_Install.md 必須涵蓋

1. Win11 VM prerequisites。
2. Visual C++ / runtime requirements（若有）。
3. Test Signing 概念。
4. Driver package 組成。
5. 如何確認 VM Snapshot 已建立。
6. 如何手動啟用必要測試環境。
7. 如何安裝 Driver。
8. 如何確認 Driver 已存在。
9. 如何確認 Driver 已啟動/Device 可開啟。
10. 如何停止/移除。
11. 如何恢復 VM。
12. 常見錯誤。
13. 事件檢視器 / SetupAPI log / Debug output 查法。

## Command Labeling Rule

所有 Driver / Boot / Verifier command 前都必須出現：

```text
MANUAL VM ONLY — DO NOT EXECUTE BY AGENT
```

## No Auto Scripts

不得提供一個 Agent 可直接執行、會自動：
- bcdedit；
- install driver；
- load driver；
- verifier；
- reboot

的 host script。

可以提供：
- build.ps1
- package.ps1

但它們只能產生檔案，不得安裝。

## Acceptance Criteria

Agent：
- package build 成功。
- INF/static package validation 可完成。

使用者：
- Fresh VM 按文件手動安裝成功。
- 可以完整 uninstall。
