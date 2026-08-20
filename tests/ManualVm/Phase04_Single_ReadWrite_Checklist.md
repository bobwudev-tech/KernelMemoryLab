> [!CAUTION]
> `MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`
>
> 下列 Driver service、device handle 與 IOCTL 操作只能由使用者本人在 Windows 11 VM 手動執行。

# Phase 04 — Manual VM Single Read/Write Checklist

## Prerequisites

- [ ] Windows 11 x64 VM snapshot 已建立。
- [ ] VM 已由使用者自行設定為可載入此 test-signed Driver。
- [ ] Release x64 的 Driver、Target 與 `KernelMemoryLab.SingleMemoryClient` 已複製到 `C:\KernelMemoryLab`。
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

## Start Target and capture current addresses

啟動 `C:\KernelMemoryLab\KernelMemoryLab.Target.exe`。從 UI 複製 PID 與五個 variable address，填入下列變數；地址只對本次 Target process lifetime 有效。

```powershell
$targetPid = 1234
$healthAddress = "0x0000000000000000"
$manaAddress = "0x0000000000000000"
$goldAddress = "0x0000000000000000"
$positionXAddress = "0x0000000000000000"
$client = "C:\KernelMemoryLab\KernelMemoryLab.SingleMemoryClient.exe"
```

## Positive reads

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
& $client read-int32 $targetPid $healthAddress 4
& $client read-int32 $targetPid $manaAddress 4
& $client read-int64 $targetPid $goldAddress 8
& $client read-float32 $targetPid $positionXAddress 4
```

- [ ] Health 回傳 `100`、BytesProcessed 為 `4`。
- [ ] Mana 回傳 `50`、BytesProcessed 為 `4`。
- [ ] Gold 回傳 `1000`、BytesProcessed 為 `8`。
- [ ] PositionX 回傳 `10`、BytesProcessed 為 `4`。

## Positive writes

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
& $client write-int32 $targetPid $healthAddress 777
Start-Sleep -Seconds 1
& $client read-int32 $targetPid $healthAddress 4

& $client write-float32 $targetPid $positionXAddress 12.5
Start-Sleep -Seconds 1
& $client read-float32 $targetPid $positionXAddress 4
```

- [ ] Target UI 在 1 秒內顯示 Health `777`。
- [ ] Health read-back 為 `777`。
- [ ] Target UI 在 1 秒內顯示 PositionX `12.5`。
- [ ] PositionX read-back 為 `12.5`。

## Negative validation matrix

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
& $client read-int32 4294967295 $healthAddress 4

$differentPid = (Get-Process explorer | Select-Object -First 1).Id
& $client read-int32 $differentPid $healthAddress 4

& $client read-raw $targetPid 0x0 4
& $client read-raw $targetPid $healthAddress 4097
& $client read-raw $targetPid 0xFFFF800000000000 4
```

- [ ] Wrong PID 回傳 `TargetNotFound`。
- [ ] Different process PID 回傳 `TargetNotAllowed`。
- [ ] Address 0 回傳 `InvalidAddress`。
- [ ] Size 4097 回傳 `InvalidSize`。
- [ ] Kernel-range address 回傳 `KernelRangeDenied`。
- [ ] 所有錯誤均安全失敗，沒有 BSOD。

## Target exit race

記住目前 `$targetPid`，關閉 Target，確認程序已退出，再執行：

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
& $client read-int32 $targetPid $healthAddress 4
```

- [ ] 回傳 `TargetNotFound` 或 `TargetExited`。
- [ ] Driver 與 VM 維持穩定。

## Cleanup

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
sc.exe stop KernelMemoryLab
sc.exe delete KernelMemoryLab
```

- [ ] Service 已停止並移除。
- [ ] 測試輸出與任何異常已回傳給 Coding Agent。
