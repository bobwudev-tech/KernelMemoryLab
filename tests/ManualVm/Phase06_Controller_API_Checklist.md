> [!CAUTION]
> `MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`
>
> 下列 Driver service、Controller device handle 與 IOCTL 操作只能由使用者本人在 Windows 11 VM 手動執行。

# Phase 06 — Manual VM Controller/API Checklist

## Prerequisites

- [ ] Windows 11 x64 VM snapshot 已建立。
- [ ] VM 已由使用者自行設定為可載入此 test-signed Driver。
- [ ] Release x64 的 Driver、Target、Controller 及其相依檔案已複製到 `C:\KernelMemoryLab`。
- [ ] 所有 Driver 命令均在 VM 的系統管理員終端執行。

## Register and start Driver

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
sc.exe create KernelMemoryLab type= kernel start= demand binPath= "C:\KernelMemoryLab\KernelMemoryLab.Driver.sys"
sc.exe start KernelMemoryLab
sc.exe query KernelMemoryLab
```

- [ ] Service 狀態為 `RUNNING`，沒有 bugcheck／BSOD。

## Start applications and connect

啟動 `KernelMemoryLab.Target.exe`，從 Target UI 記錄 PID 與 Health、Mana、Gold、PositionX、PositionY 地址。

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
& "C:\KernelMemoryLab\KernelMemoryLab.Controller.exe"
```

在 Controller 點選 `Connect`。

- [ ] 顯示 `Driver Connected`。
- [ ] Protocol 為 `1.0`。
- [ ] Driver version 為 `0.5.0.0`。
- [ ] Capabilities 為 `0x0000000000000F07`。
- [ ] Operation details 顯示 Connect、DriverStatus、Win32Error、timestamp。

## Single read

在 Controller 輸入 Target PID、Health address、`Int32`，點選 `Read`。

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

- [ ] Result 與 Target UI Health 相同；全新 Target 為 `100`。
- [ ] Operation details 包含 `Operation=ReadSingle`、`DriverStatus=Success`、Target PID 與 timestamp。

## Single write

輸入 Health address、`Int32`、值 `777`，點選 `Write`。

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

- [ ] Controller 顯示 `Success, 4 bytes`。
- [ ] Target UI 在一秒內更新 Health 為 `777`。
- [ ] 再次 Single Read 回傳 `777`。

## Batch read

將五個 rows 設為：Health/Int32、Mana/Int32、Gold/Int64、PositionX/Float32、PositionY/Float32，貼上各自地址後點選 `Read Batch`。

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

- [ ] 五個 row 均顯示 `Success` 與正確 bytes。
- [ ] Read Value 與 Target UI 的五個值一致。
- [ ] Overall status 為 `Success`，BytesProcessed 為 `24`。

## Batch write

在 Health、Mana、Gold rows 的 Write Value 輸入 `700`、`70`、`1700`。其餘兩列輸入目前的 Float32 值，點選 `Write Batch`。

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

- [ ] 五個 per-item status 均為 `Success`。
- [ ] Target UI 在一秒內反映 Health `700`、Mana `70`、Gold `1700`。
- [ ] Batch Read 可讀回相同值。

## Controller-side negative validation

以下輸入應由 Controller/API 在送出 IOCTL 前拒絕：

- [ ] Address `not-hex`：Operation details 顯示 malformed input，Controller 不 crash。
- [ ] Address `0x0`：顯示 nonzero validation error。
- [ ] PID `0`：顯示 PID validation error。
- [ ] 清空一個 batch address 或 typed value：顯示具 operation、PID、timestamp 的錯誤。

## Driver-side negative status UX

輸入已退出 Target 的舊 PID，執行 Single Read。

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

- [ ] Operation details 顯示 operation、`TargetNotFound` 或 `TargetExited`、Win32 error 欄位、舊 PID 與 timestamp。
- [ ] Controller 與 VM 保持穩定。

## Disconnect and cleanup

先在 Controller 點選 `Disconnect` 並關閉 Controller/Target。

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
sc.exe stop KernelMemoryLab
sc.exe delete KernelMemoryLab
```

- [ ] Controller 顯示 `Driver Disconnected`。
- [ ] Service 已停止並移除。
- [ ] 保存畫面與 Operation details，將任何非預期狀態或 VM instability 回傳給 Coding Agent。
