# KernelMemoryLab Controller Driver API — V1

## Scope and version

`KernelMemoryApi` 是 `KernelMemoryLab.Controller.exe` 使用的 V1 user-mode wrapper。它只為受控 `KernelMemoryLab.Target.exe` 的 user virtual memory 建立既有 protocol request，不提供任意 process、kernel memory、physical memory 或 security bypass 能力。

| Item | V1 value |
|---|---|
| Namespace | `KernelMemoryLab.Controller.DriverApi` |
| API type | `KernelMemoryApi` |
| Protocol / Driver | `1.0` / `0.5.0.0` |
| Device path | `\\.\KernelMemoryLab` |
| Single / batch limits | `4096` bytes / `128` items / `524288` aggregate bytes |
| Capability mask | `0x0000000000000F07` |

```text
ControllerViewModel -> KernelMemoryApi -> IDriverTransport
                                      -> CreateFile / DeviceIoControl
```

`IDriverTransport` 可注入，因此 unit tests 使用 in-memory fake，不會開啟真實 device。預設 constructor 使用 `Win32DriverTransport`。

> [!CAUTION]
> `MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`
>
> 使用預設 transport 呼叫 `Open()` 或從 Controller 點選 Connect，會開啟真實 Driver device；只能由使用者本人在 Windows 11 VM 手動執行。

## Common lifecycle, validation and errors

- 一個 `KernelMemoryApi` instance 擁有一個 transport/device handle。除 `Open`、`Close`、`IsOpen` 外，所有操作都要求 handle 已開啟。
- API 與預設 transport **不保證 thread-safe**；不要從多個 thread 同時使用同一 instance。建議每個 Controller/session 使用一個 instance、序列化操作並以 `using`/`Dispose` 收尾。
- Target PID/address 只在同一次 `KernelMemoryLab.Target.exe` lifetime 有效；Target 重啟後必須全部重新取得。
- Controller-side validation 是 early failure/UX，不是安全邊界。Driver 仍會驗證 PID、image identity、lifecycle、range、size 與 offsets。
- `DriverApiException` 提供 `Operation`、`DriverStatus`、`Win32Error`、`TargetProcessId`、`DetailStatus` 與 `Timestamp`。
- 常見 status：`InvalidPid`、`TargetNotFound`、`TargetNotAllowed`、`InvalidAddress`、`InvalidSize`、`AddressRangeOverflow`、`KernelRangeDenied`、`MemoryNotAccessible`、`TargetExited`、`InvalidItemCount`、`InvalidOffset`、`AggregateLimitExceeded`。

## Connection and protocol APIs

### `Open`

```csharp
void Open()
```

- **Purpose:** 開啟 `\\.\KernelMemoryLab` 或注入 transport。
- **Parameters:** 無。
- **Return type:** `void`。
- **Validation:** 預設 transport 已開啟時直接返回；否則以 read/write access 開啟既有 device。
- **Errors:** `CreateFile` Win32 error 或 transport state error 會包成 `DriverApiException`，`Operation = "Open"`。
- **Thread-safety/lifetime:** session 開始時呼叫一次；不可與 `Close`/`Dispose` 或其他 operation concurrent 呼叫。

```csharp
using KernelMemoryApi api = new();
api.Open();
```

### `Close`

```csharp
void Close()
```

- **Purpose:** 關閉 transport/device handle。
- **Parameters:** 無。
- **Return type:** `void`。
- **Validation:** 無 protocol request；預設 transport 可重複關閉。
- **Errors:** 預設 transport 的 dispose 通常不拋出；自訂 transport 依其實作。
- **Thread-safety/lifetime:** 所有 in-flight operation 完成後呼叫；關閉後其他 API 會以「device is not open」失敗。

```csharp
api.Close();
```

### `GetProtocolVersion`

```csharp
GetProtocolVersionResponse GetProtocolVersion()
```

- **Purpose:** 讀取 Driver protocol version。
- **Parameters:** 無。
- **Return type:** `GetProtocolVersionResponse`；版本在 `Header.ProtocolVersion`。
- **Validation:** 建立 version `1.0`、正確 structure size、zero flags/reserved 的 request；要求已 `Open`。
- **Errors:** transport failure、response size 不符或 Driver status 非 `Success` 時拋出 `DriverApiException`。
- **Thread-safety/lifetime:** handle 開啟期間序列呼叫；不修改 Target。

```csharp
GetProtocolVersionResponse response = api.GetProtocolVersion();
ushort major = response.Header.ProtocolVersion.Major;
```

### `GetCapabilities`

```csharp
GetCapabilitiesResponse GetCapabilities()
```

- **Purpose:** 取得 capability mask 與 single/batch limits。
- **Parameters:** 無。
- **Return type:** `GetCapabilitiesResponse`，含 `Capabilities`、`MaxSingleItemSize`、`MaxBatchItems`、`MaxBatchPayloadSize`。
- **Validation:** 固定 V1 request；要求已 `Open` 且 exact response size。
- **Errors:** transport failure、malformed response 或 non-success status 會拋出 `DriverApiException`。
- **Thread-safety/lifetime:** 序列呼叫；不同 Driver build 不可沿用舊 capabilities 假設。

```csharp
GetCapabilitiesResponse response = api.GetCapabilities();
uint maxItems = response.MaxBatchItems;
```

### `Ping`

```csharp
PingResponse Ping(ulong token = 0x0123456789ABCDEFUL)
```

- **Purpose:** 驗證 round trip，取得 Driver version/capabilities/echo。
- **Parameters:** `token` — 任意 64-bit opaque value。
- **Return type:** `PingResponse`。
- **Validation:** 要求已 `Open`、status `Success` 且 `EchoToken` 等於輸入。
- **Errors:** transport/protocol failure或 token mismatch 會拋出 `DriverApiException`。
- **Thread-safety/lifetime:** handle 開啟期間序列呼叫；不修改 Target。

```csharp
PingResponse response = api.Ping(0x0123456789ABCDEFUL);
DriverVersion version = response.DriverVersion;
```

## Raw single-item APIs

### `Read`

```csharp
ReadSingleMessage Read(uint targetProcessId, ulong address, uint size)
```

- **Purpose:** 從受控 Target user VA 讀取 raw bytes。
- **Parameters:** `targetProcessId` — 當前 Target PID；`address` — Target UI 顯示的 VA；`size` — `1..4096`。
- **Return type:** `ReadSingleMessage`，包含 `Header` 與 `Data`。
- **Validation:** Controller 先拒絕 PID `0`/System、address `0`、zero/oversized size、overflow 與 kernel range；Driver 再驗證 PID/image/lifecycle/range/pages。
- **Errors:** validation、transport、decode、partial 或其他 non-success status 都會拋出 `DriverApiException`。
- **Thread-safety/lifetime:** 只可使用當前 Target lifetime 的 PID/address；不要與 Target shutdown 或同 instance 其他操作 concurrent 執行。

```csharp
ReadSingleMessage response = api.Read(targetPid, healthAddress, sizeof(int));
ReadOnlyMemory<byte> bytes = response.Data;
```

### `Write`

```csharp
WriteSingleResponse Write(uint targetProcessId, ulong address, ReadOnlySpan<byte> data)
```

- **Purpose:** 將 raw inline bytes 寫入受控 Target user VA。
- **Parameters:** PID/address 同 `Read`；`data` 為 `1..4096` bytes。
- **Return type:** `WriteSingleResponse`；成功時 `BytesProcessed == data.Length`。
- **Validation:** 依 `data.Length` 做與 `Read` 相同的 early validation；Driver 重新 authoritative validation。
- **Errors:** empty/oversized data、invalid target/range、transport failure或 Driver non-success status 都拋出 `DriverApiException`。
- **Thread-safety/lifetime:** span 在 method 返回後不保留；只寫 Target UI 明列的 variable range並序列化操作。

```csharp
byte[] value = BitConverter.GetBytes(777);
WriteSingleResponse response = api.Write(targetPid, healthAddress, value);
```

## Batch APIs

### `ReadBatch`

```csharp
BatchReadResponseMessage ReadBatch(
    uint targetProcessId,
    IReadOnlyList<BatchReadRequestItem> requests)
```

- **Purpose:** 對同一受控 Target PID 一次讀取多個 ranges。
- **Parameters:** `targetProcessId`；`requests` 為 `1..128` items，每項含 `Address`/`Size`。
- **Return type:** `BatchReadResponseMessage`，含 overall header、per-item `Results` 與 inline `Data`。
- **Validation:** list 不可 null/empty/>128；每項通過 single validation；encoder 使用 checked aggregate/offset arithmetic。Driver 在 access 前驗證完整 envelope 與 target。
- **Errors:** fatal envelope/transport/decode status 拋出 `DriverApiException`；`PartialTransfer`/`AllItemsFailed` 正常返回，caller 必須逐項檢查。
- **Thread-safety/lifetime:** 全部地址必須屬於同一 PID/lifetime；呼叫期間不修改 list，同 instance 序列使用。

```csharp
BatchReadResponseMessage response = api.ReadBatch(
    targetPid,
    [
        new BatchReadRequestItem(healthAddress, sizeof(int)),
        new BatchReadRequestItem(goldAddress, sizeof(long)),
    ]);

foreach (BatchItemResult item in response.Results)
    Console.WriteLine($"{item.OperationStatus}: {item.BytesProcessed}");
```

### `WriteBatch`

```csharp
BatchWriteResponseMessage WriteBatch(
    uint targetProcessId,
    IReadOnlyList<BatchWriteRequestItem> requests)
```

- **Purpose:** 對同一受控 Target PID 一次寫入多個 inline payloads。
- **Parameters:** `targetProcessId`；`requests` 為 `1..128` items，每項含 `Address`/non-empty `Data`。
- **Return type:** `BatchWriteResponseMessage`，含 overall header 與 per-item `Results`。
- **Validation:** list/count/range/每項 size/aggregate offsets 經 checked validation；Driver 再驗證 envelope 與每項。
- **Errors:** fatal envelope/transport/decode status 拋出 `DriverApiException`；partial/all-failed 正常返回並要求逐項檢查。
- **Thread-safety/lifetime:** data 在呼叫中複製進 flat buffer；地址須屬同一 Target lifetime，同 instance 不可 concurrent 使用。

```csharp
byte[] health = new byte[sizeof(int)];
byte[] gold = new byte[sizeof(long)];
BinaryPrimitives.WriteInt32LittleEndian(health, 777);
BinaryPrimitives.WriteInt64LittleEndian(gold, 1234);

BatchWriteResponseMessage response = api.WriteBatch(
    targetPid,
    [
        new BatchWriteRequestItem(healthAddress, health),
        new BatchWriteRequestItem(goldAddress, gold),
    ]);
```

## Typed read helpers

Typed helpers 使用 little-endian signed integers 與 IEEE 754 binary32，並要求 exactly 固定 byte count。

### `ReadInt32`

```csharp
int ReadInt32(uint targetProcessId, ulong address)
```

- **Purpose:** 讀取 4-byte signed integer。
- **Parameters:** 當前 Target PID 與 Int32 address。
- **Return type:** `int`。
- **Validation:** 等同 `Read(..., 4)`，並要求 data exactly 4 bytes。
- **Errors:** `Read` 的錯誤或 response length mismatch 皆為 `DriverApiException`。
- **Thread-safety/lifetime:** address 必須在當前 Target lifetime 有效；同 instance 序列使用。

```csharp
int health = api.ReadInt32(targetPid, healthAddress);
```

### `ReadInt64`

```csharp
long ReadInt64(uint targetProcessId, ulong address)
```

- **Purpose:** 讀取 8-byte signed integer。
- **Parameters:** 當前 Target PID 與 Int64 address。
- **Return type:** `long`。
- **Validation:** 等同 `Read(..., 8)`，並要求 data exactly 8 bytes。
- **Errors:** `Read` 的錯誤或 response length mismatch。
- **Thread-safety/lifetime:** 同 `ReadInt32`。

```csharp
long gold = api.ReadInt64(targetPid, goldAddress);
```

### `ReadFloat32`

```csharp
float ReadFloat32(uint targetProcessId, ulong address)
```

- **Purpose:** 讀取 4-byte IEEE 754 binary32。
- **Parameters:** 當前 Target PID 與 Float32 address。
- **Return type:** `float`。
- **Validation:** 等同 `Read(..., 4)`，exact length 後依 little-endian bits 轉換。
- **Errors:** `Read` 的錯誤或 response length mismatch。
- **Thread-safety/lifetime:** 同 `ReadInt32`。

```csharp
float x = api.ReadFloat32(targetPid, positionXAddress);
```

## Typed write helpers

### `WriteInt32`

```csharp
WriteSingleResponse WriteInt32(uint targetProcessId, ulong address, int value)
```

- **Purpose:** 寫入 4-byte little-endian signed integer。
- **Parameters:** 當前 Target PID、Int32 address、`int value`。
- **Return type:** `WriteSingleResponse`。
- **Validation:** 編碼 4 bytes 後委派給 `Write`；Controller/Driver 仍驗證 PID/range/size。
- **Errors:** `Write` 的所有 `DriverApiException`。
- **Thread-safety/lifetime:** 只用當前 Target lifetime 的 Int32 address；序列使用。

```csharp
WriteSingleResponse response = api.WriteInt32(targetPid, healthAddress, 777);
```

### `WriteInt64`

```csharp
WriteSingleResponse WriteInt64(uint targetProcessId, ulong address, long value)
```

- **Purpose:** 寫入 8-byte little-endian signed integer。
- **Parameters:** 當前 Target PID、Int64 address、`long value`。
- **Return type:** `WriteSingleResponse`。
- **Validation:** 編碼 8 bytes 後委派給 `Write`。
- **Errors:** `Write` 的所有 `DriverApiException`。
- **Thread-safety/lifetime:** 同 `WriteInt32`，address 必須對應 Int64 variable。

```csharp
WriteSingleResponse response = api.WriteInt64(targetPid, goldAddress, 1234L);
```

### `WriteFloat32`

```csharp
WriteSingleResponse WriteFloat32(uint targetProcessId, ulong address, float value)
```

- **Purpose:** 以 IEEE 754 bits 寫入 4-byte Float32。
- **Parameters:** 當前 Target PID、Float32 address、`float value`。
- **Return type:** `WriteSingleResponse`。
- **Validation:** 以 little-endian bits 編碼後委派給 `Write`。
- **Errors:** `Write` 的所有 `DriverApiException`。
- **Thread-safety/lifetime:** 同 `WriteInt32`，address 必須對應 Float32 variable。

```csharp
WriteSingleResponse response = api.WriteFloat32(targetPid, positionXAddress, 25.5f);
```

## Complete minimal lifecycle

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

```csharp
using KernelMemoryApi api = new();
api.Open();

try
{
    GetProtocolVersionResponse protocol = api.GetProtocolVersion();
    GetCapabilitiesResponse capabilities = api.GetCapabilities();
    PingResponse ping = api.Ping();
    int health = api.ReadInt32(targetPid, healthAddress);
    api.WriteInt32(targetPid, healthAddress, health + 1);
}
catch (DriverApiException error)
{
    Console.Error.WriteLine(
        $"{error.Operation}: Status={error.DriverStatus}, " +
        $"Win32={error.Win32Error}, PID={error.TargetProcessId}, " +
        $"Detail=0x{error.DetailStatus:X8}, Time={error.Timestamp:O}");
}
finally
{
    api.Close();
}
```

真實 Driver acceptance 只能依 `docs/VM_Test_Checklist.md` 由使用者在 Windows 11 VM 手動執行。
