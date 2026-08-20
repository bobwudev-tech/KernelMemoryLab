using System.Runtime.InteropServices;

namespace KernelMemoryLab.Protocol;

public sealed record WriteSingleMessage(
    WriteSingleRequestHeader Header,
    ReadOnlyMemory<byte> Data);

public sealed record ReadSingleMessage(
    CommonResponseHeader Header,
    ReadOnlyMemory<byte> Data);

public static class SingleMemoryProtocol
{
    public const int RequestHeaderSize = 32;
    public const int ResponseHeaderSize = 16;

    public static byte[] EncodeWriteRequest(
        uint targetProcessId,
        ulong address,
        ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty || data.Length > ProtocolConstants.MaxSingleItemSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(data),
                $"Data length must be between 1 and {ProtocolConstants.MaxSingleItemSize} bytes.");
        }

        int totalSize = checked(RequestHeaderSize + data.Length);
        WriteSingleRequestHeader request = new()
        {
            Header = new CommonRequestHeader
            {
                ProtocolVersion = ProtocolConstants.CurrentProtocolVersion,
                StructureSize = checked((uint)totalSize),
                Flags = 0,
                Reserved = 0,
            },
            TargetProcessId = targetProcessId,
            Size = checked((uint)data.Length),
            Address = address,
        };

        byte[] buffer = new byte[totalSize];
        MemoryMarshal.Write(buffer.AsSpan(0, RequestHeaderSize), in request);
        data.CopyTo(buffer.AsSpan(RequestHeaderSize));
        return buffer;
    }

    public static WriteSingleMessage DecodeWriteRequest(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < RequestHeaderSize)
        {
            throw new ArgumentException(
                $"Write request requires at least {RequestHeaderSize} bytes.",
                nameof(buffer));
        }

        WriteSingleRequestHeader header = MemoryMarshal.Read<WriteSingleRequestHeader>(buffer);
        int expectedSize = checked(RequestHeaderSize + checked((int)header.Size));
        if (header.Header.StructureSize != expectedSize || buffer.Length != expectedSize)
        {
            throw new InvalidDataException("Write request size fields do not match its payload.");
        }

        return new WriteSingleMessage(header, buffer[RequestHeaderSize..].ToArray());
    }

    public static byte[] EncodeReadResponse(
        CommonResponseHeader header,
        ReadOnlySpan<byte> data)
    {
        if (data.Length > ProtocolConstants.MaxSingleItemSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(data),
                $"Read data cannot exceed {ProtocolConstants.MaxSingleItemSize} bytes.");
        }

        if (header.BytesProcessed != data.Length)
        {
            throw new ArgumentException(
                "BytesProcessed must equal the supplied data length.",
                nameof(header));
        }

        byte[] buffer = new byte[checked(ResponseHeaderSize + data.Length)];
        MemoryMarshal.Write(buffer.AsSpan(0, ResponseHeaderSize), in header);
        data.CopyTo(buffer.AsSpan(ResponseHeaderSize));
        return buffer;
    }

    public static ReadSingleMessage DecodeReadResponse(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < ResponseHeaderSize)
        {
            throw new ArgumentException(
                $"Read response requires at least {ResponseHeaderSize} bytes.",
                nameof(buffer));
        }

        CommonResponseHeader header = MemoryMarshal.Read<CommonResponseHeader>(buffer);
        if (header.BytesProcessed > ProtocolConstants.MaxSingleItemSize)
        {
            throw new InvalidDataException("Read response exceeds the single-item limit.");
        }

        int expectedSize = checked(ResponseHeaderSize + checked((int)header.BytesProcessed));
        if (buffer.Length != expectedSize)
        {
            throw new InvalidDataException("Read response length does not match BytesProcessed.");
        }

        return new ReadSingleMessage(header, buffer[ResponseHeaderSize..].ToArray());
    }
}

public static class SingleMemoryRequestValidator
{
    public const ulong MaximumX64UserAddress = 0x00007FFFFFFFFFFFUL;
    public const ulong MinimumX64KernelAddress = 0xFFFF800000000000UL;

    public static OperationStatus Validate(
        uint targetProcessId,
        ulong address,
        uint size,
        ulong highestUserAddress = MaximumX64UserAddress,
        ulong systemRangeStart = MinimumX64KernelAddress,
        uint systemProcessId = 4)
    {
        if (highestUserAddress >= systemRangeStart)
        {
            throw new ArgumentException("User and system address ranges overlap.");
        }

        if (targetProcessId == 0 || targetProcessId == systemProcessId)
        {
            return OperationStatus.InvalidPid;
        }

        if (address == 0)
        {
            return OperationStatus.InvalidAddress;
        }

        if (size == 0 || size > ProtocolConstants.MaxSingleItemSize)
        {
            return OperationStatus.InvalidSize;
        }

        if (address > ulong.MaxValue - size)
        {
            return OperationStatus.AddressRangeOverflow;
        }

        ulong lastAddress = checked(address + size - 1UL);
        if (address >= systemRangeStart || lastAddress >= systemRangeStart)
        {
            return OperationStatus.KernelRangeDenied;
        }

        if (address > highestUserAddress || lastAddress > highestUserAddress)
        {
            return OperationStatus.InvalidAddress;
        }

        return OperationStatus.Success;
    }
}
