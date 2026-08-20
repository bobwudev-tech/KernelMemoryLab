# KernelMemoryLab Protocol 1.0

## Scope

Phase 02 implements only:

- `GET_PROTOCOL_VERSION`
- `GET_CAPABILITIES`
- `PING`

`READ_SINGLE`, `WRITE_SINGLE`, `READ_BATCH`, and `WRITE_BATCH` have reserved IOCTL numbers but are not implemented and are not reported as capabilities.

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
| READ_SINGLE (reserved) | `0x810` | `0x0022E040` |
| WRITE_SINGLE (reserved) | `0x811` | `0x0022E044` |
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

## Validation

The Driver validates exact input structure size, protocol version, zero flags, and zero reserved fields before processing. Protocol-level rejections return a current-version response with a non-success `OperationStatus`; buffers smaller than the common header are rejected by WDF before parsing.

