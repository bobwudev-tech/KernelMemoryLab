> [!CAUTION]
> `MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`
>
> 下列 Driver service、device handle 與 IOCTL 操作只能由使用者本人在 Windows 11 VM 手動執行。

# Phase 05 — Manual VM Batch Read/Write Checklist

## Prerequisites

- [ ] Windows 11 x64 VM snapshot 已建立。
- [ ] VM 已由使用者自行設定為可載入此 test-signed Driver。
- [ ] Release x64 的 Driver、Target 與 `KernelMemoryLab.BatchMemoryClient.exe` 已複製到 `C:\KernelMemoryLab`。
- [ ] 所有 Driver 命令均在 VM 的系統管理員終端執行。

## Register and start Driver

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
sc.exe create KernelMemoryLab type= kernel start= demand binPath= "C:\KernelMemoryLab\KernelMemoryLab.Driver.sys"
sc.exe start KernelMemoryLab
sc.exe query KernelMemoryLab
```

- [ ] Service 狀態為 `RUNNING`。
- [ ] 系統沒有 bugcheck／BSOD。

## Start Target and capture addresses

啟動 `C:\KernelMemoryLab\KernelMemoryLab.Target.exe`。從 UI 複製 PID 與五個 variable address；地址只對本次 Target process lifetime 有效。

```powershell
$targetPid = 1234
$healthAddress = "0x0000000000000000"
$manaAddress = "0x0000000000000000"
$goldAddress = "0x0000000000000000"
$positionXAddress = "0x0000000000000000"
$positionYAddress = "0x0000000000000000"
$client = "C:\KernelMemoryLab\KernelMemoryLab.BatchMemoryClient.exe"
```

## Read five variables in one request

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
& $client read-five $targetPid $healthAddress $manaAddress $goldAddress $positionXAddress $positionYAddress
```

- [ ] Overall status 為 `Success`，五個 per-item status 均為 `Success`。
- [ ] 全新 Target 的值為 Health `100`、Mana `50`、Gold `1000`、PositionX `10`、PositionY `20`。
- [ ] Overall BytesProcessed 為 `24`（4 + 4 + 8 + 4 + 4）。

## Write three variables and read back

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
& $client write-three $targetPid $healthAddress $manaAddress $goldAddress 777 75 1234
Start-Sleep -Seconds 1
& $client read-five $targetPid $healthAddress $manaAddress $goldAddress $positionXAddress $positionYAddress
```

- [ ] Write overall status 為 `Success`，三個 per-item status 均為 `Success`。
- [ ] Target UI 在一秒內更新為 Health `777`、Mana `75`、Gold `1234`。
- [ ] Batch read-back 回傳上述三個值，PositionX 與 PositionY 維持不變。

## Invalid middle item continues safely

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
& $client read-invalid-middle $targetPid $healthAddress $manaAddress
```

- [ ] Overall status 為 `PartialTransfer`。
- [ ] 第一與第三項為 `Success`；中間項為 `InvalidAddress` 且 BytesProcessed 為零。
- [ ] Driver 在 invalid item 後繼續處理，沒有 crash 或 BSOD。

## Request-envelope rejection

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
& $client too-many $targetPid $healthAddress
& $client malformed-offset $targetPid $healthAddress
```

- [ ] 129-item declaration 在 memory access 前以 `InvalidItemCount` 拒絕。
- [ ] Malformed items offset 在 memory access 前以 `InvalidOffset` 拒絕。
- [ ] 兩者均安全回傳，沒有 crash 或 BSOD。

## Cleanup

關閉 Target，然後執行：

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
sc.exe stop KernelMemoryLab
sc.exe delete KernelMemoryLab
```

- [ ] Service 已停止並移除。
- [ ] 保存命令輸出，將任何非預期 status 或 VM instability 回傳給 Coding Agent。
