namespace KernelMemoryLab.Protocol;

/// <summary>
/// IOCTL constants use FILE_DEVICE_UNKNOWN, METHOD_BUFFERED, and read/write access.
/// Defining a future IOCTL does not mean that the operation is implemented.
/// </summary>
public static class IoControlCodes
{
    private const uint FileDeviceUnknown = 0x22;
    private const uint MethodBuffered = 0;
    private const uint FileReadWriteAccess = 0x0003;

    public const uint GetProtocolVersion =
        (FileDeviceUnknown << 16) | (FileReadWriteAccess << 14) |
        ((uint)ProtocolOperation.GetProtocolVersion << 2) | MethodBuffered;

    public const uint GetCapabilities =
        (FileDeviceUnknown << 16) | (FileReadWriteAccess << 14) |
        ((uint)ProtocolOperation.GetCapabilities << 2) | MethodBuffered;

    public const uint Ping =
        (FileDeviceUnknown << 16) | (FileReadWriteAccess << 14) |
        ((uint)ProtocolOperation.Ping << 2) | MethodBuffered;

    public const uint ReadSingle =
        (FileDeviceUnknown << 16) | (FileReadWriteAccess << 14) |
        ((uint)ProtocolOperation.ReadSingle << 2) | MethodBuffered;

    public const uint WriteSingle =
        (FileDeviceUnknown << 16) | (FileReadWriteAccess << 14) |
        ((uint)ProtocolOperation.WriteSingle << 2) | MethodBuffered;

    public const uint ReadBatch =
        (FileDeviceUnknown << 16) | (FileReadWriteAccess << 14) |
        ((uint)ProtocolOperation.ReadBatch << 2) | MethodBuffered;

    public const uint WriteBatch =
        (FileDeviceUnknown << 16) | (FileReadWriteAccess << 14) |
        ((uint)ProtocolOperation.WriteBatch << 2) | MethodBuffered;
}

