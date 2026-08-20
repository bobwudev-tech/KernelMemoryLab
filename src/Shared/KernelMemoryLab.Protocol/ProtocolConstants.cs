namespace KernelMemoryLab.Protocol;

/// <summary>
/// Stable limits and identifiers for protocol version 1.0.
/// </summary>
public static class ProtocolConstants
{
    public const ushort ProtocolMajor = 1;
    public const ushort ProtocolMinor = 0;

    public const ushort DriverMajor = 0;
    public const ushort DriverMinor = 5;
    public const ushort DriverBuild = 0;
    public const ushort DriverRevision = 0;

    public const uint MaxSingleItemSize = 4_096;
    public const uint MaxBatchItems = 128;
    public const uint MaxBatchPayloadSize = MaxSingleItemSize * MaxBatchItems;

    public const ProtocolCapabilities Phase02Capabilities =
        ProtocolCapabilities.GetProtocolVersion |
        ProtocolCapabilities.GetCapabilities |
        ProtocolCapabilities.Ping;

    public const ProtocolCapabilities Phase04Capabilities =
        Phase02Capabilities |
        ProtocolCapabilities.ReadSingle |
        ProtocolCapabilities.WriteSingle;

    public const ProtocolCapabilities Phase05Capabilities =
        Phase04Capabilities |
        ProtocolCapabilities.ReadBatch |
        ProtocolCapabilities.WriteBatch;

    public const string DevicePath = @"\\.\KernelMemoryLab";

    public static ProtocolVersion CurrentProtocolVersion => new(ProtocolMajor, ProtocolMinor);

    public static DriverVersion CurrentDriverVersion =>
        new(DriverMajor, DriverMinor, DriverBuild, DriverRevision);
}
