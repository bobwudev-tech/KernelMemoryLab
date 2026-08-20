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


# AGENTS.md — KernelMemoryLab Mandatory Rules

## Rule 1 — Agent never runs the kernel driver

無論 Agent 認為目前是在：
- Host
- VM
- Remote machine
- CI
- Sandbox

都不得自行安裝、載入、啟動、停止、呼叫或驗證 KernelMemoryLab.Driver.sys。

本專案所有 Kernel Integration Test 均為 **USER-MANUAL-VM-ONLY**。

## Rule 2 — Allowed commands

Agent 可執行：
- Git-related non-destructive development commands。
- Package restore。
- Build / Compile。
- Static Analysis。
- Code generation。
- User-mode-only unit tests。
- Target self-memory unit tests。
- Mock Driver transport tests。

## Rule 3 — Forbidden commands

禁止自行執行：
- pnputil driver installation/removal。
- sc.exe kernel service create/start/stop/delete。
- devcon driver actions。
- bcdedit。
- testsigning changes。
- DeviceIoControl against real KernelMemoryLab device。
- Driver Verifier。
- WinDbg kernel attach。
- reboot for driver testing。
- security-feature disablement。
- any `.sys` load mechanism。

## Rule 4 — Manual commands

若目前 Phase 需要 Driver 測試：
1. Agent 產生精確步驟。
2. 標註 `MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`。
3. 等使用者提供結果。
4. Agent 根據結果分析/修改。
5. Agent 不得因為「測試方便」自行執行。

## Rule 5 — Memory Scope

V1 只能實作：
- `KernelMemoryLab.Target.exe`
- User Virtual Memory
- Single / Batch R/W

不得自行擴大成：
- arbitrary process；
- kernel memory；
- physical memory；
- security/protection bypass。

## Rule 6 — Phase Discipline

每次工作：
1. 先閱讀 `00_Project_Contract.md`。
2. 再閱讀當前 Phase。
3. 只實作當前 Phase。
4. Build。
5. 執行 Agent Allowed Tests。
6. 輸出 Build/Test Summary。
7. 若需要 VM test，停止自動執行並輸出 Manual VM Checklist。
