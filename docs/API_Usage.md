# KernelMemoryLab Controller Driver API

## Scope

`KernelMemoryApi` is the Phase 06 user-mode wrapper used by `KernelMemoryLab.Controller.exe`. It only builds the existing V1 protocol for the controlled `KernelMemoryLab.Target.exe` user virtual-memory lab. The kernel Driver independently revalidates PID, image identity, ranges, sizes, and offsets.

The WPF UI never constructs IOCTL buffers. Its call path is:

```text
ControllerViewModel
    -> KernelMemoryApi
        -> IDriverTransport
            -> Win32 CreateFile / DeviceIoControl
```

`IDriverTransport` is injectable so API and ViewModel tests use an in-memory fake and never open the real device.

## Lifecycle and connection information

```csharp
using KernelMemoryLab.Controller.DriverApi;

using KernelMemoryApi api = new();
api.Open();

GetProtocolVersionResponse protocol = api.GetProtocolVersion();
GetCapabilitiesResponse capabilities = api.GetCapabilities();
PingResponse ping = api.Ping();

bool connected = api.IsOpen;
api.Close();
```

The Controller connect action calls all three information methods. It displays protocol version, Driver version, capability mask, and connected/disconnected state.

## Single operations

```csharp
ReadSingleMessage raw = api.Read(pid, address, size);
WriteSingleResponse written = api.Write(pid, address, data);

int health = api.ReadInt32(pid, healthAddress);
long gold = api.ReadInt64(pid, goldAddress);
float x = api.ReadFloat32(pid, positionXAddress);

api.WriteInt32(pid, healthAddress, 777);
api.WriteInt64(pid, goldAddress, 1234);
api.WriteFloat32(pid, positionXAddress, 12.5f);
```

Typed helpers use little-endian `Int32`, `Int64`, and IEEE 754 `Float32` representations.

## Batch operations

```csharp
BatchReadResponseMessage read = api.ReadBatch(
    pid,
    [
        new BatchReadRequestItem(healthAddress, sizeof(int)),
        new BatchReadRequestItem(goldAddress, sizeof(long)),
    ]);

BatchWriteResponseMessage write = api.WriteBatch(
    pid,
    [
        new BatchWriteRequestItem(healthAddress, healthBytes),
        new BatchWriteRequestItem(goldAddress, goldBytes),
    ]);
```

One PID applies to the entire batch. Overall `Success`, `PartialTransfer`, and `AllItemsFailed` responses retain all per-item statuses for the caller to inspect.

## Controller-side validation

Before invoking the transport, the API rejects PID zero/System PID, address zero, user-range overflow, kernel-range addresses, zero size, items larger than 4096 bytes, empty batches, and batches over 128 items. The UI additionally rejects malformed decimal/hex PID, address, and typed values.

This validation is a usability boundary, not a security boundary. The Driver always repeats authoritative validation.

## Errors

`DriverApiException` carries:

- operation name;
- protocol/Driver status when available;
- Win32 error when transport open or invocation fails;
- target PID when applicable;
- Driver detail status;
- timestamp.

The Controller renders those fields in the Operation details panel instead of showing a generic failure message.

## Real Driver use

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

Opening the default `KernelMemoryApi` transport or clicking Controller `Connect` opens `\\.\KernelMemoryLab`; subsequent API operations call the real Driver. Perform this only by following `tests/ManualVm/Phase06_Controller_API_Checklist.md` inside the user-controlled Windows 11 VM.
