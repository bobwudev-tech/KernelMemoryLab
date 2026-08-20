# KernelMemoryLab Protocol 1.0

## Scope

Through Phase 04 the Driver implements:

- `GET_PROTOCOL_VERSION`
- `GET_CAPABILITIES`
- `PING`
- `READ_SINGLE`
- `WRITE_SINGLE`

`READ_BATCH` and `WRITE_BATCH` retain reserved IOCTL numbers but are not implemented and are not reported as capabilities.

The Phase 04 capability mask is `0x0000000000000307`.

## Wire format

All messages are fixed-width, little-endian, packed, flat structures. They contain no nested pointers, function pointers, kernel addresses, or physical addresses.

Common request header (16 bytes):

| Offset | Type | Field |
|---:|---|---|
| 0 | `UInt16` | Protocol major |
| 2 | `UInt16` | Protocol minor |
| 4 | `UInt32` | Structure size |
| 8 | `UInt32` | Flags; must be zero in 1.0 |
| 12 | `UInt32` | Reserved; must be zero |

Common response header (16 bytes):

| Offset | Type | Field |
|---:|---|---|
| 0 | `UInt16` | Protocol major |
| 2 | `UInt16` | Protocol minor |
| 4 | `UInt32` | Operation status |
| 8 | `UInt32` | Bytes processed |
| 12 | `UInt32` | Detail status (`NTSTATUS` bit pattern) |

## IOCTLs

All codes use `FILE_DEVICE_UNKNOWN`, `METHOD_BUFFERED`, and `FILE_READ_ACCESS | FILE_WRITE_ACCESS`.

| Operation | Function | IOCTL |
|---|---:|---:|
| GET_PROTOCOL_VERSION | `0x800` | `0x0022E000` |
| GET_CAPABILITIES | `0x801` | `0x0022E004` |
| PING | `0x802` | `0x0022E008` |
| READ_SINGLE | `0x810` | `0x0022E040` |
| WRITE_SINGLE | `0x811` | `0x0022E044` |
| READ_BATCH (reserved) | `0x812` | `0x0022E048` |
| WRITE_BATCH (reserved) | `0x813` | `0x0022E04C` |

## Limits

- Protocol version: `1.0`.
- Single item: `4096` bytes maximum.
- Batch: `128` items maximum.
- Batch aggregate payload: `524288` bytes maximum.
- Future addresses: `UInt64`.
- Future sizes and offsets: fixed-width unsigned integers.
- Implementations must use checked size, offset, and aggregate calculations before buffer access.

## Single-item wire format

Read request (32 bytes):

| Offset | Type | Field |
|---:|---|---|
| 0 | 16 bytes | Common request header; StructureSize must be 32 |
| 16 | `UInt32` | Target PID |
| 20 | `UInt32` | Size |
| 24 | `UInt64` | User virtual address |

Read response consists of the 16-byte common response header followed immediately by `BytesProcessed` data bytes. A successful response is exactly `16 + requested size` bytes.

Write request:

| Offset | Type | Field |
|---:|---|---|
| 0 | 16 bytes | Common request header; StructureSize is total request length |
| 16 | `UInt32` | Target PID |
| 20 | `UInt32` | Size |
| 24 | `UInt64` | User virtual address |
| 32 | byte array | Exactly Size bytes of inline data |

Write response is one 16-byte common response header. All single-item messages are flat `METHOD_BUFFERED` messages and contain no nested user pointers.

## Single-item operation statuses

| Value | Status | Meaning |
|---:|---|---|
| 1 | `ProtocolMismatch` | Request protocol version is unsupported |
| 8 | `InvalidRequest` | Actual buffer length and declared payload disagree |
| 9 | `InvalidPid` | PID is zero or the System process |
| 10 | `TargetNotFound` | PID does not resolve to a live process |
| 11 | `TargetNotAllowed` | Image basename is not exactly `KernelMemoryLab.Target.exe` |
| 12 | `InvalidAddress` | Zero, non-user, or otherwise invalid user-range address |
| 13 | `InvalidSize` | Size is zero or greater than 4096 |
| 14 | `AddressRangeOverflow` | Address plus size overflows UInt64 |
| 15 | `KernelRangeDenied` | Any byte enters the system address range |
| 16 | `MemoryNotAccessible` | User pages cannot be probed, locked, or mapped |
| 17 | `PartialTransfer` | Fewer bytes transferred than requested |
| 18 | `TargetExited` | Target exited during validation or transfer |

## Validation

The Driver validates exact input structure size, protocol version, zero flags, and zero reserved fields before processing. Protocol-level rejections return a current-version response with a non-success `OperationStatus`; buffers smaller than the common header are rejected by WDF before parsing.

Before single memory access, the Driver rejects PID zero and the System process, resolves a referenced `PEPROCESS`, confirms the process is still active, and compares the full image basename case-insensitively with `KernelMemoryLab.Target.exe`. It then validates nonzero size/address, the 4096-byte limit, UInt64 overflow, `MmHighestUserAddress`, and `MmSystemRangeStart`.

The WDF device and queue run the handler at PASSIVE_LEVEL. The transfer attaches only to the referenced Target process, probes and locks the requested user pages through an MDL, maps those locked pages into system space, performs the copy, and always unlocks/detaches/releases resources. Probe exceptions and exit races are converted to protocol statuses rather than escaping the handler.

