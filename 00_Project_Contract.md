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


# 00 — KernelMemoryLab Project Contract

## 1. 專案目的

以規格驅動開發（Specification-Driven Development, SDD）方式，建立一套 Windows User Process Memory 讀寫實驗系統。

本專案使用 Kernel-mode Driver 作為記憶體存取執行者，但 **V1 的存取目標只允許 User Process Virtual Memory**。

最終功能成品固定為：

```text
KernelMemoryLab.Driver.sys
KernelMemoryLab.Target.exe
KernelMemoryLab.Controller.exe
```

## 2. 最終架構

```text
┌──────────────────────────────┐
│ KernelMemoryLab.Target.exe   │
│                              │
│ unmanaged test memory        │
│ Health / Mana / Gold / X / Y │
│ UI 每秒重新讀取並刷新          │
└──────────────▲───────────────┘
               │
               │ User Process Virtual Memory
               │
┌──────────────┴───────────────┐
│ KernelMemoryLab.Driver.sys   │
│ Kernel-mode (KMDF)           │
│ Single Read / Write          │
│ Batch Read / Write           │
└──────────────▲───────────────┘
               │ IOCTL
┌──────────────┴───────────────┐
│ KernelMemoryLab.Controller   │
│ .exe                         │
│ Driver API Wrapper + WPF UI  │
└──────────────────────────────┘
```

## 3. V1 Memory Scope

### 允許
- 由 Driver 讀取 `KernelMemoryLab.Target.exe` 的 User Virtual Memory。
- 由 Driver 寫入 `KernelMemoryLab.Target.exe` 的 User Virtual Memory。
- 單一位址讀取。
- 單一位址寫入。
- 多位址 Batch Read。
- 多位址 Batch Write。
- Int32 / Int64 / Float 等基本型別測試。
- 合理錯誤輸入的拒絕與錯誤回報。

### 明確禁止
V1 不得包含：
- 任意 Kernel Virtual Address R/W。
- Physical Memory R/W。
- CR3 / Page Table walking/manipulation。
- MSR 操作。
- Kernel patch。
- 任意 Process R/W。
- PID 0 / System process。
- Protected Process / PPL bypass。
- Anti-cheat / Security product bypass。
- Callback removal。
- Handle protection bypass。
- Manual Mapping。
- Driver hiding。
- Code injection。
- 任意可泛化為繞過第三方保護機制的能力。

## 4. 為何使用 Kernel Driver

本專案的研究重點是理解：

```text
User App
  ↓ IOCTL
Kernel Driver
  ↓
User Process Virtual Memory
```

而不是研究 Windows Kernel Memory 本身。

Driver 位於 Kernel-mode，只代表「執行存取的程式碼位於 Kernel」，不代表被存取的資料位於 Kernel Address Space。

## 5. 測試目標限制

V1 Driver 僅接受：

```text
KernelMemoryLab.Target.exe
```

Driver 必須驗證：
- PID 存在。
- Process Image Name 符合指定 Lab Target。
- 非 System process。
- Address 為 User-mode canonical address。
- Address + Length 不 overflow。
- Length 在 protocol 限制內。
- Request 不進入 Kernel address range。

未通過驗證時，必須拒絕要求。

## 6. Agent / VM 分工

### Coding Agent
只負責：
- Spec
- Source Code
- Build
- Static Analysis
- User-mode-only Unit Tests
- Documentation
- VM Manual Test Script/Checklist 的產生

### 使用者本人
只在 Windows 11 VM 中手動負責：
- Test Signing
- Driver 安裝
- Driver 載入/卸載
- Controller → Driver IOCTL 實測
- Read/Write Integration Test
- Driver Verifier
- BSOD / Dump 驗證
- Driver 移除

Agent **不得** SSH/Remote 到 VM 幫忙執行上述測試。

## 7. Repository Layout

```text
KernelMemoryLab/
├─ src/
│  ├─ Driver/
│  │  └─ KernelMemoryLab.Driver/
│  ├─ Target/
│  │  └─ KernelMemoryLab.Target/
│  ├─ Controller/
│  │  └─ KernelMemoryLab.Controller/
│  └─ Shared/
│     └─ KernelMemoryLab.Protocol/
├─ tests/
│  ├─ Unit/
│  └─ ManualVm/
├─ docs/
│  ├─ Driver_Install.md
│  ├─ API_Usage.md
│  ├─ VM_Test_Checklist.md
│  └─ Verification_Report.md
├─ scripts/
│  └─ build.ps1
├─ AGENTS.md
└─ README.md
```

## 8. Definition of Done

專案只有在使用者於 VM 手動驗證以下項目後才算完成：

1. `.sys` Build 成功。
2. Driver Package 可在 Win11 VM 手動安裝。
3. Target.exe 顯示至少 5 個固定生命週期的 unmanaged variables。
4. Target UI 每秒重新讀取 memory 並刷新。
5. Controller 可 Open Driver。
6. Single Read 正確。
7. Single Write 正確，Target UI 可看到新值。
8. Batch Read 正確。
9. Batch Write 正確。
10. Wrong PID / Address / Size / Kernel Range 等錯誤要求安全失敗。
11. Driver_Install.md 完整。
12. API_Usage.md 完整。
13. VM_Test_Checklist.md 完整。
14. Verification_Report.md 由使用者測試結果填寫。
