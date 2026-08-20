> [!CAUTION]
> `MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`
>
> 下列所有 Driver service 與 PING 操作只能由使用者本人在 Windows 11 VM 手動執行。

# Phase 02 — Manual VM PING Checklist

## Prerequisites

- [ ] Windows 11 x64 VM snapshot 已建立。
- [ ] VM 已由使用者自行設定為可載入此 test-signed Driver。
- [ ] 已將 `KernelMemoryLab.Driver.sys` 與 `KernelMemoryLab.PingClient` Release 輸出複製到 `C:\KernelMemoryLab`。
- [ ] 所有命令均在 VM 的系統管理員終端執行。

## Register and start

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
sc.exe create KernelMemoryLab type= kernel start= demand binPath= "C:\KernelMemoryLab\KernelMemoryLab.Driver.sys"
sc.exe start KernelMemoryLab
sc.exe query KernelMemoryLab
```

- [ ] Service 狀態為 `RUNNING`。
- [ ] 系統保持穩定，沒有 bugcheck／BSOD。

## Protocol checks

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
& "C:\KernelMemoryLab\KernelMemoryLab.PingClient.exe" version
& "C:\KernelMemoryLab\KernelMemoryLab.PingClient.exe" capabilities
& "C:\KernelMemoryLab\KernelMemoryLab.PingClient.exe" ping 0x0123456789ABCDEF
```

- [ ] Protocol version 為 `1.0`。
- [ ] Capabilities mask 為 `0x0000000000000007`。
- [ ] Limits 為 single `4096`、batch items `128`、aggregate `524288`。
- [ ] PING status 為 `Success`。
- [ ] Driver version 為 `0.2.0.0`。
- [ ] Echo token 為 `0x0123456789ABCDEF`。

## Negative checks

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
& "C:\KernelMemoryLab\KernelMemoryLab.PingClient.exe" version-mismatch
& "C:\KernelMemoryLab\KernelMemoryLab.PingClient.exe" malformed-size
```

- [ ] Version mismatch 回傳 `UnsupportedProtocolVersion`。
- [ ] Malformed structure 回傳 `InvalidStructureSize`。
- [ ] 兩項錯誤輸入均安全失敗，沒有 BSOD。

## Cleanup

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```powershell
sc.exe stop KernelMemoryLab
sc.exe delete KernelMemoryLab
```

- [ ] Service 已停止並移除。
- [ ] 測試結果已記錄並回傳給 Coding Agent。

