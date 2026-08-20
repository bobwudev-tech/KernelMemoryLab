namespace KernelMemoryLab.Protocol;

public enum ProtocolOperation : uint
{
    GetProtocolVersion = 0x800,
    GetCapabilities = 0x801,
    Ping = 0x802,

    // Reserved for later phases. Phase 02 does not implement these operations.
    ReadSingle = 0x810,
    WriteSingle = 0x811,
    ReadBatch = 0x812,
    WriteBatch = 0x813,
}

public enum OperationStatus : uint
{
    Success = 0,
    UnsupportedProtocolVersion = 1,
    InvalidStructureSize = 2,
    InvalidFlags = 3,
    InvalidReservedField = 4,
    UnsupportedOperation = 5,
    BufferTooSmall = 6,
    InternalError = 7,
}

[Flags]
public enum ProtocolCapabilities : ulong
{
    None = 0,
    GetProtocolVersion = 1UL << 0,
    GetCapabilities = 1UL << 1,
    Ping = 1UL << 2,

    // These bits are reserved and are intentionally not returned in Phase 02.
    ReadSingle = 1UL << 8,
    WriteSingle = 1UL << 9,
    ReadBatch = 1UL << 10,
    WriteBatch = 1UL << 11,
}

