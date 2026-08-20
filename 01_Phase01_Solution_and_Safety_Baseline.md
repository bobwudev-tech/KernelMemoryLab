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


# Phase 01 — Solution & Safety Baseline

## Objective

建立 KernelMemoryLab solution、Repository Layout、Build Pipeline 以及不可被 Agent 越過的 Driver Safety Policy。

## Deliverables

```text
KernelMemoryLab.sln
src/Driver/KernelMemoryLab.Driver
src/Target/KernelMemoryLab.Target
src/Controller/KernelMemoryLab.Controller
src/Shared/KernelMemoryLab.Protocol
tests/Unit
tests/ManualVm
AGENTS.md
.agent/skills/kernel-driver-safety/SKILL.md
scripts/build.ps1
```

## Requirements

### Driver
- x64。
- KMDF。
- 此 Phase 只需 Skeleton，不能實作 memory R/W。
- 可編譯即可。

### Target / Controller
- C# WPF。
- 建立空殼專案。
- 可正常 Build。

### Shared Protocol
建立共用 Protocol 定義位置，但 Phase 01 不定義完整 IOCTL。

## Build Policy

Agent 可以執行：

```text
restore
build
compile
static analysis
```

Agent 不可以因為 Build 成功後自動：
- 安裝 INF。
- 呼叫 PnPUtil。
- 建立 Kernel Service。
- 啟動 Driver。
- 呼叫 DeviceIoControl。

`build.ps1` 必須保證只 Build，不安裝、不執行 Driver。

## Acceptance Criteria

Agent 可以自行驗證：
- Solution restore 成功。
- x64 build 成功。
- 無自動 driver deployment step。
- `AGENTS.md` 與 Safety Skill 已存在。

使用者 VM 驗證：
- 本 Phase 不要求 Driver 實際載入。

## Exit Criteria

只有 Build Baseline 與 Safety Policy 都完成後才能開始 Phase 02。
