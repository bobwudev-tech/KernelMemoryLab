using System.Runtime.InteropServices;

namespace KernelMemoryLab.Protocol;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ProtocolVersion
{
    public ProtocolVersion(ushort major, ushort minor)
    {
        Major = major;
        Minor = minor;
    }

    public ushort Major;
    public ushort Minor;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct DriverVersion
{
    public DriverVersion(ushort major, ushort minor, ushort build, ushort revision)
    {
        Major = major;
        Minor = minor;
        Build = build;
        Revision = revision;
    }

    public ushort Major;
    public ushort Minor;
    public ushort Build;
    public ushort Revision;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct CommonRequestHeader
{
    public ProtocolVersion ProtocolVersion;
    public uint StructureSize;
    public uint Flags;
    public uint Reserved;

    public static CommonRequestHeader Create<TRequest>()
        where TRequest : unmanaged
    {
        return new CommonRequestHeader
        {
            ProtocolVersion = ProtocolConstants.CurrentProtocolVersion,
            StructureSize = checked((uint)Marshal.SizeOf<TRequest>()),
            Flags = 0,
            Reserved = 0,
        };
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct CommonResponseHeader
{
    public ProtocolVersion ProtocolVersion;
    public OperationStatus OperationStatus;
    public uint BytesProcessed;
    public uint DetailStatus;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct GetProtocolVersionRequest
{
    public CommonRequestHeader Header;

    public static GetProtocolVersionRequest Create() =>
        new() { Header = CommonRequestHeader.Create<GetProtocolVersionRequest>() };
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct GetProtocolVersionResponse
{
    public CommonResponseHeader Header;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct GetCapabilitiesRequest
{
    public CommonRequestHeader Header;

    public static GetCapabilitiesRequest Create() =>
        new() { Header = CommonRequestHeader.Create<GetCapabilitiesRequest>() };
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct GetCapabilitiesResponse
{
    public CommonResponseHeader Header;
    public ProtocolCapabilities Capabilities;
    public uint MaxSingleItemSize;
    public uint MaxBatchItems;
    public uint MaxBatchPayloadSize;
    public uint Reserved;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PingRequest
{
    public CommonRequestHeader Header;
    public ulong Token;

    public static PingRequest Create(ulong token) =>
        new()
        {
            Header = CommonRequestHeader.Create<PingRequest>(),
            Token = token,
        };
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PingResponse
{
    public CommonResponseHeader Header;
    public DriverVersion DriverVersion;
    public ProtocolCapabilities Capabilities;
    public ulong EchoToken;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ReadSingleRequest
{
    public CommonRequestHeader Header;
    public uint TargetProcessId;
    public uint Size;
    public ulong Address;

    public static ReadSingleRequest Create(uint targetProcessId, ulong address, uint size) =>
        new()
        {
            Header = CommonRequestHeader.Create<ReadSingleRequest>(),
            TargetProcessId = targetProcessId,
            Size = size,
            Address = address,
        };
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct WriteSingleRequestHeader
{
    public CommonRequestHeader Header;
    public uint TargetProcessId;
    public uint Size;
    public ulong Address;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct WriteSingleResponse
{
    public CommonResponseHeader Header;
}

