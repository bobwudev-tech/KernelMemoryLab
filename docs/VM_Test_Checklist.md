> [!CAUTION]
> `MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`
>
> 本文件內所有 Driver 安裝、service control、device open、IOCTL、Driver Verifier、重新開機與 VM 操作，只能由使用者本人在可復原的 Windows 11 x64 VM 手動執行。Coding Agent 不得執行這些步驟。

# KernelMemoryLab Phase 08 — Windows 11 VM Integration Checklist

## 1. Scope and current status

本 checklist 驗證 V1 的受控 `KernelMemoryLab.Target.exe` user virtual memory single/batch read/write。不得改用任意 process、kernel virtual address、physical memory、CR3/page table、protected process 或任何 security bypass 作為成功案例。

目前狀態：**MANUAL VM RESULTS PENDING**。建立本文件不代表任何真實 Driver integration test 已通過。

- 執行順序固定為 T01 至 T09；T10 為使用者自行決定的 optional advanced test。
- 每一項測試都必須填寫結果並保存輸出或畫面。
- 任一項出現 BSOD、hang、unexpected reboot 或資料破壞時立即停止，不要重複相同操作。
- 僅執行本文件列出的有限負向案例；不得進行 unbounded fuzzing。

## 2. Test artifacts

Phase 07 Release package 應包含：

```text
Driver\KernelMemoryLab.Driver.sys
Driver\KernelMemoryLab.Driver.inf
Driver\kernelmemorylab.driver.cat
Driver\KernelMemoryLab.Driver.cer
Controller\KernelMemoryLab.Controller.exe
Target\KernelMemoryLab.Target.exe
PackageManifest.json
```

T02、T04、T05、T06、T07、T08、T09 另使用三個已限制用途的 manual harness。它們只能對本 Lab Driver 執行規格化測試：

```text
KernelMemoryLab.PingClient.exe
KernelMemoryLab.SingleMemoryClient.exe
KernelMemoryLab.BatchMemoryClient.exe
```

這三個專案可由 Agent Build，但其 EXE 只能由使用者在 Manual VM 執行。Release Build 輸出位於各自的：

```text
tests\ManualVm\<project>\bin\x64\Release\net8.0-windows\
```

建議 VM 目錄：

```text
C:\KernelMemoryLab\Package\
C:\KernelMemoryLab\Tools\Ping\
C:\KernelMemoryLab\Tools\Single\
C:\KernelMemoryLab\Tools\Batch\
C:\KernelMemoryLab\Evidence\
```

## 3. Environment record and pre-test gate

在任何 Driver 或 boot state 變更之前完成：

- [ ] 建立 VM snapshot，名稱：`____________________________`
- [ ] 確認 snapshot 可復原，且 VM 內沒有重要資料。
- [ ] 確認 VM 為隔離的 Windows 11 x64 測試環境。
- [ ] 記錄 `winver` 顯示的 edition、version 與 OS build。
- [ ] 記錄 Secure Boot 狀態。
- [ ] 確認已安裝 .NET 8 Desktop Runtime x64。
- [ ] 複製單一 Release package；本輪不得混用其他 build 的 SYS/INF/CAT/EXE。
- [ ] 複製三個 manual harness 及各自完整輸出目錄。
- [ ] 使用 `PackageManifest.json` 驗證 package 內所有檔案 SHA-256 均一致。
- [ ] 依 `docs/Driver_Install.md` 完成 test-signing 與 certificate prerequisites。
- [ ] 建立 `C:\KernelMemoryLab\Evidence` 儲存文字輸出與截圖。

環境欄位：

```text
Test date/time:
Tester:
VM product:
Snapshot name:
Windows edition/version/build:
Architecture: x64
Secure Boot state:
Package directory:
PackageManifest SHA-256:
Driver version: 0.5.0.0
Protocol version: 1.0
Target build/commit:
Controller build/commit:
Notes:
```

## 4. Common result format

每個 Test ID 都複製並填寫以下區塊。不要只填總結：

```text
Test ID:
Build / PackageManifest SHA-256:
VM OS:
Driver Version:
Target Version:
Controller Version:
Start Time:
End Time:
Result: PASS / FAIL / BLOCKED / NOT RUN
Observed:
Expected:
OperationStatus / Error Code:
Evidence files:
Notes:
```

建議證據檔名：`Txx-short-description-YYYYMMDD-HHmmss.txt` 或 `.png`。

## 5. T01 — Driver package, service and device

依 `docs/Driver_Install.md` 第 5 至第 10 節執行 package inspection、certificate trust、primitive Driver installation、service query/start 與 Controller device open。不要混用舊的 `sc.exe create` 安裝方式。

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

- [ ] Package manifest 全部 hash 驗證成功。
- [ ] INF、SYS 與 CAT 都來自同一個 package。
- [ ] `pnputil /enum-drivers` 可找到對應 Published Name，並已記錄正確 `oem#.inf`。
- [ ] `sc.exe qc KernelMemoryLab` 顯示 kernel driver、demand start 與預期 binary path。
- [ ] `sc.exe start KernelMemoryLab` 成功。
- [ ] `sc.exe query KernelMemoryLab` 顯示 `RUNNING`。
- [ ] 啟動 Controller 並點選 `Connect` 後顯示 `Driver Connected`。
- [ ] VM 沒有 BSOD、hang 或 unexpected reboot。

Expected：Driver version `0.5.0.0`；Controller 可開啟 `\\.\KernelMemoryLab`。

T01 Result：`NOT RUN`

## 6. T02 — Protocol, version and capabilities

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
$pingClient = "C:\KernelMemoryLab\Tools\Ping\KernelMemoryLab.PingClient.exe"
& $pingClient version
& $pingClient capabilities
& $pingClient ping 0x0123456789ABCDEF
```

- [ ] 三個命令 exit code 均為 `0`。
- [ ] Protocol version 為 `1.0`。
- [ ] Driver version 為 `0.5.0.0`。
- [ ] Capabilities mask 為 `0x0000000000000F07`。
- [ ] `MaxSingleItemSize = 4096`。
- [ ] `MaxBatchItems = 128`。
- [ ] `MaxBatchPayloadSize = 524288`。
- [ ] PING status 為 `Success`。
- [ ] Echo token 為 `0x0123456789ABCDEF`。
- [ ] VM 維持穩定。

T02 Result：`NOT RUN`

## 7. T03 — Controlled Target

從本輪 package 的 `Target` 目錄啟動 `KernelMemoryLab.Target.exe`，不要改用其他程式作為成功讀寫目標。

- [ ] UI 顯示 process image `KernelMemoryLab.Target.exe`。
- [ ] 記錄 PID。
- [ ] 記錄 Health、Mana、Gold、PositionX、PositionY 五個地址。
- [ ] 初始值依序為 `100`、`50`、`1000`、`10`、`20`。
- [ ] 觀察至少三次 UI refresh；時間戳或值顯示約每秒更新且 UI 可回應。

填入本次 process-lifetime-only 資料：

```powershell
$targetPid = 0
$healthAddress = "0x0000000000000000"
$manaAddress = "0x0000000000000000"
$goldAddress = "0x0000000000000000"
$positionXAddress = "0x0000000000000000"
$positionYAddress = "0x0000000000000000"

$singleClient = "C:\KernelMemoryLab\Tools\Single\KernelMemoryLab.SingleMemoryClient.exe"
$batchClient = "C:\KernelMemoryLab\Tools\Batch\KernelMemoryLab.BatchMemoryClient.exe"
```

> 關閉或重新啟動 Target 後，這些 PID/address 立即失效，必須重新抄錄。

T03 Result：`NOT RUN`

## 8. T04 — Single read

保持 Target 執行，逐一讀取五個已顯示的地址。

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
& $singleClient read-int32   $targetPid $healthAddress    4
& $singleClient read-int32   $targetPid $manaAddress      4
& $singleClient read-int64   $targetPid $goldAddress      8
& $singleClient read-float32 $targetPid $positionXAddress 4
& $singleClient read-float32 $targetPid $positionYAddress 4
```

- [ ] 每項 `OperationStatus = Success`。
- [ ] BytesProcessed 依序為 `4`、`4`、`8`、`4`、`4`。
- [ ] 五個 read value 與執行當下 Target UI 完全相符。
- [ ] 沒有讀取任何未列在 Target UI 的地址。
- [ ] VM 與 Target 維持穩定。

T04 Result：`NOT RUN`

## 9. T05 — Single write and UI refresh

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
& $singleClient write-int32   $targetPid $healthAddress    777
& $singleClient write-int32   $targetPid $manaAddress      123
& $singleClient write-float32 $targetPid $positionXAddress 25.5
Start-Sleep -Seconds 2

& $singleClient read-int32   $targetPid $healthAddress    4
& $singleClient read-int32   $targetPid $manaAddress      4
& $singleClient read-float32 $targetPid $positionXAddress 4
```

- [ ] 三個 write 均為 `Success`，BytesProcessed 均為 `4`。
- [ ] Target UI 在下一次 refresh 顯示 Health `777`、Mana `123`、PositionX `25.5`。
- [ ] read-back 分別為 `777`、`123`、`25.5`。
- [ ] Gold 與 PositionY 沒有被非預期修改。
- [ ] VM 與 Target 維持穩定。

T05 Result：`NOT RUN`

## 10. T06 — Batch read

一次讀取全部五個 test variables。

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
& $batchClient read-five `
    $targetPid `
    $healthAddress `
    $manaAddress `
    $goldAddress `
    $positionXAddress `
    $positionYAddress
```

- [ ] Overall status 為 `Success`。
- [ ] 五個 per-item status 都是 `Success`。
- [ ] Overall BytesProcessed 為 `24`。
- [ ] 五個值與 Target UI 相符；若緊接 T05，應為 `777`、`123`、`1000`、`25.5`、`20`。
- [ ] VM 與 Target 維持穩定。

T06 Result：`NOT RUN`

## 11. T07 — Batch write and read-back

一次更新 Health、Mana、Gold，再以 T06 的五項 batch read 完整讀回。

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
& $batchClient write-three `
    $targetPid `
    $healthAddress `
    $manaAddress `
    $goldAddress `
    701 `
    71 `
    1701

Start-Sleep -Seconds 2

& $batchClient read-five `
    $targetPid `
    $healthAddress `
    $manaAddress `
    $goldAddress `
    $positionXAddress `
    $positionYAddress
```

- [ ] Batch write overall status 為 `Success`。
- [ ] 三個 write per-item status 均為 `Success`。
- [ ] Target UI 在下一次 refresh 顯示 `701`、`71`、`1701`。
- [ ] Batch read-back 五個 per-item status 均為 `Success`。
- [ ] PositionX 與 PositionY 保持 T07 前的值。
- [ ] VM 與 Target 維持穩定。

T07 Result：`NOT RUN`

## 12. T08 — Bounded negative validation

每一個案例只執行一次。預期非零 harness exit code 表示 Driver 正確拒絕，不應誤判為 harness failure；以輸出的 `OperationStatus` 判定。拒絕後確認 Target 五個值未改變。

### T08.1 Wrong PID

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
& $singleClient read-int32 4294967295 $healthAddress 4
```

Expected：`TargetNotFound`，或等價的安全拒絕；不得 memory access。

### T08.2 Non-target process PID

僅用於驗證 image allowlist；不得把非 Target process 當成成功讀寫目標。

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
$nonTargetPid = (Get-Process explorer | Select-Object -First 1).Id
& $singleClient read-int32 $nonTargetPid $healthAddress 4
```

Expected：`TargetNotAllowed`，且在 memory access 前拒絕。

### T08.3 Address and size validation

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
& $singleClient read-raw $targetPid 0x0                    4
& $singleClient read-raw $targetPid 0x00007FFFFFFF0000     4
& $singleClient read-raw $targetPid $healthAddress         0
& $singleClient read-raw $targetPid $healthAddress         4097
& $singleClient read-raw $targetPid 0xFFFF800000000000     4
```

Expected：

- [ ] Address `0`：`InvalidAddress`。
- [ ] Unmapped user VA：`MemoryNotAccessible` 或等價的 safe failure。
- [ ] Zero size：`InvalidSize`。
- [ ] Size `4097`：`InvalidSize`。
- [ ] Kernel-range address：`KernelRangeDenied`，且在 access 前拒絕。

### T08.4 Batch envelope validation

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
& $batchClient too-many        $targetPid $healthAddress
& $batchClient malformed-offset $targetPid $healthAddress
```

Expected：129 items 為 `InvalidItemCount`；錯誤 offset 為 `InvalidOffset`；兩者都在 memory access 前拒絕。

### T08.5 Protocol version and structure validation

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
& $pingClient version-mismatch
& $pingClient malformed-size
```

Expected：`ProtocolMismatch` 與 `InvalidStructureSize`。

### T08.6 Exited Target PID and stale addresses

記錄 `$oldTargetPid` 與五個舊地址，正常關閉 Target，確認 process 已退出後只執行一次讀取。

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
$oldTargetPid = $targetPid
& $singleClient read-int32 $oldTargetPid $healthAddress 4
```

Expected：`TargetNotFound` 或 `TargetExited`。不得繼續對舊 PID/address 操作。完成後重新啟動 Target，重新記錄新 PID 與全部五個地址。

T08 acceptance：

- [ ] 所有案例均符合預期 OperationStatus 或已記錄實際 safe-failure status。
- [ ] Driver、Controller、Target 與 VM 均未 crash/hang。
- [ ] 沒有非預期 memory modification。
- [ ] 沒有 unbounded fuzzing。

T08 Result：`NOT RUN`

## 13. T09 — Bounded repeated normal operations

使用重新啟動後的新 PID/address。下列次數是固定上限，不得改成無限迴圈。

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
1..25 | ForEach-Object {
    & $singleClient read-int32 $targetPid $healthAddress 4
    if ($LASTEXITCODE -ne 0) { throw "Single read failed at iteration $_" }
}

1..10 | ForEach-Object {
    & $batchClient read-five `
        $targetPid `
        $healthAddress `
        $manaAddress `
        $goldAddress `
        $positionXAddress `
        $positionYAddress
    if ($LASTEXITCODE -ne 0) { throw "Batch read failed at iteration $_" }
}
```

再由 Controller 執行最多 10 組 bounded single write/read-back 與最多 10 組 batch write/read-back，值只寫入五個已記錄的 Target variables。

- [ ] 所有 normal operation 均為 `Success`。
- [ ] Target UI 與 read-back 值一致。
- [ ] Controller/Target working set 沒有持續、明顯的無界成長。
- [ ] Driver service 維持 `RUNNING`。
- [ ] Event Viewer 沒有本 Driver 引起的新 critical/error event。
- [ ] VM 沒有 crash、hang 或 unexpected reboot。
- [ ] 再次重新啟動 Target 時，Controller 不沿用舊 PID/address；新資料重新輸入後操作成功。

T09 Result：`NOT RUN`

## 14. T10 — Driver Verifier (optional / advanced)

T10 不屬於基本 Phase 08 PASS 的必要條件。只有使用者明確決定承擔較高的 bugcheck 風險時才執行，而且必須已建立可復原 snapshot、只指定本 Driver，並準備好從 snapshot 復原。

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
verifier.exe /standard /driver KernelMemoryLab.Driver.sys
verifier.exe /querysettings
Restart-Computer
```

重新開機後只重跑 T02、T04、T05、T06、T07 與一組 T09 bounded operations，不做額外 fuzzing。完成或需要停用時：

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
verifier.exe /reset
Restart-Computer
```

- [ ] Verifier 僅列出 `KernelMemoryLab.Driver.sys`。
- [ ] 測試完成後已執行 reset，或已恢復 snapshot。
- [ ] 若發生 BSOD，已保存 dump/bugcheck 資料並停止重測。

T10 Result：`NOT RUN / SKIPPED`（擇一；SKIPPED 不影響基本 Phase 08 結果）

## 15. Cleanup and restoration

先在 Controller 點選 `Disconnect`，再關閉 Controller 與 Target。依 `docs/Driver_Install.md` 第 11、12 節，使用 T01 記錄的精確 `oem#.inf` 停止/移除 Driver package、移除本 package 的 test certificate、關閉 test-signing 並恢復 snapshot。

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

- [ ] Controller 已 Disconnect 並關閉。
- [ ] Target 已關閉。
- [ ] Driver service 已停止。
- [ ] 正確的 Driver package 已移除。
- [ ] `sc.exe query KernelMemoryLab` 回報 service 不存在。
- [ ] 僅移除本次 package 的 test certificate，thumbprint 已核對。
- [ ] 若曾啟用 Driver Verifier，已 reset 或恢復 snapshot。
- [ ] 若曾啟用 test-signing，已依安裝指南關閉並重新開機。
- [ ] VM 已恢復到 pre-test snapshot，或已記錄不復原的理由。

## 16. Failure workflow

若任何項目 FAIL/BLOCKED：

1. 立即停止後續測試；不要自行擴大案例或重複可能造成 BSOD 的輸入。
2. 記錄最後一個 Test ID、完整命令/操作、Observed、Expected、OperationStatus、Win32 error 與時間。
3. 保存 Controller `Operation details`、PowerShell 輸出、Event Viewer System/CodeIntegrity events，以及 `%SystemRoot%\inf\setupapi.dev.log` 的相關片段。
4. 若 BSOD，保存 bugcheck code、四個 parameters 與 dump；不要在同一 VM 立刻重跑。
5. 恢復 snapshot。
6. 將下列 failure handoff 貼回給 Coding Agent。Agent 只會分析、修改、Build 與更新文件，不會操作 VM。

```text
Failed Test ID:
PackageManifest SHA-256:
Windows version/build:
Last successful Test ID:
Exact command/UI action:
Observed:
Expected:
OperationStatus:
Win32 error / NTSTATUS:
Bugcheck code and parameters:
Evidence filenames:
Can reproduce from clean snapshot: YES / NO / NOT RETRIED
Notes:
```

## 17. Final summary

只有 T01–T09 全部 PASS，且 cleanup/restore 完成，Phase 08 基本驗證才可標記 PASS。T10 可為 SKIPPED。

| Test | Result | Evidence | Notes |
|---|---|---|---|
| T01 Driver package/service/device | NOT RUN | | |
| T02 Protocol | NOT RUN | | |
| T03 Target | NOT RUN | | |
| T04 Single read | NOT RUN | | |
| T05 Single write | NOT RUN | | |
| T06 Batch read | NOT RUN | | |
| T07 Batch write | NOT RUN | | |
| T08 Negative validation | NOT RUN | | |
| T09 Repeated normal operations | NOT RUN | | |
| T10 Driver Verifier (optional) | NOT RUN / SKIPPED | | |
| Cleanup / restore | NOT RUN | | |

```text
Overall Phase 08 Result: PENDING MANUAL VM VERIFICATION
Tester:
Completion date/time:
PackageManifest SHA-256:
Open failures:
Additional notes:
```
