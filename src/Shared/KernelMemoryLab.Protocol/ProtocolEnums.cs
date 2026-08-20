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
    ProtocolMismatch = 1,
    InvalidStructureSize = 2,
    InvalidFlags = 3,
    InvalidReservedField = 4,
    UnsupportedOperation = 5,
    BufferTooSmall = 6,
    InternalError = 7,
    InvalidRequest = 8,
    InvalidPid = 9,
    TargetNotFound = 10,
    TargetNotAllowed = 11,
    InvalidAddress = 12,
    InvalidSize = 13,
    AddressRangeOverflow = 14,
    KernelRangeDenied = 15,
    MemoryNotAccessible = 16,
    PartialTransfer = 17,
    TargetExited = 18,
}

[Flags]
public enum ProtocolCapabilities : ulong
{
    None = 0,
    GetProtocolVersion = 1UL << 0,
    GetCapabilities = 1UL << 1,
    Ping = 1UL << 2,

    // Single-item operations are enabled in Phase 04.
    ReadSingle = 1UL << 8,
    WriteSingle = 1UL << 9,

    // Batch operations remain reserved until Phase 05.
    ReadBatch = 1UL << 10,
    WriteBatch = 1UL << 11,
}

