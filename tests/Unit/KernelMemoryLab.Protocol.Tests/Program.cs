using System.Buffers.Binary;
using System.Runtime.InteropServices;
using KernelMemoryLab.Protocol;

namespace KernelMemoryLab.Protocol.Tests;

internal static class Program
{
    private static readonly (string Name, Action Test)[] Tests =
    {
        (nameof(StructureSizesAreStable), StructureSizesAreStable),
        (nameof(IoControlCodesAreStable), IoControlCodesAreStable),
        (nameof(PingSerializationIsStable), PingSerializationIsStable),
        (nameof(SerializationRoundTrips), SerializationRoundTrips),
        (nameof(DeserializeRejectsWrongSize), DeserializeRejectsWrongSize),
        (nameof(LimitsAndCapabilitiesAreSafe), LimitsAndCapabilitiesAreSafe),
        (nameof(SingleRequestWireFormatIsStable), SingleRequestWireFormatIsStable),
        (nameof(SingleResponseRoundTrips), SingleResponseRoundTrips),
        (nameof(SingleRequestValidationRejectsUnsafeRanges), SingleRequestValidationRejectsUnsafeRanges),
        (nameof(SingleOperationStatusesAreStable), SingleOperationStatusesAreStable),
    };

    private static int Main()
    {
        int failures = 0;

        foreach ((string name, Action test) in Tests)
        {
            try
            {
                test();
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception exception)
            {
                failures++;
                Console.Error.WriteLine($"FAIL {name}: {exception.Message}");
            }
        }

        Console.WriteLine($"Protocol tests: {Tests.Length - failures} passed, {failures} failed.");
        return failures == 0 ? 0 : 1;
    }

    private static void StructureSizesAreStable()
    {
        AssertEqual(4, Marshal.SizeOf<ProtocolVersion>());
        AssertEqual(8, Marshal.SizeOf<DriverVersion>());
        AssertEqual(16, Marshal.SizeOf<CommonRequestHeader>());
        AssertEqual(16, Marshal.SizeOf<CommonResponseHeader>());
        AssertEqual(16, Marshal.SizeOf<GetProtocolVersionRequest>());
        AssertEqual(16, Marshal.SizeOf<GetProtocolVersionResponse>());
        AssertEqual(16, Marshal.SizeOf<GetCapabilitiesRequest>());
        AssertEqual(40, Marshal.SizeOf<GetCapabilitiesResponse>());
        AssertEqual(24, Marshal.SizeOf<PingRequest>());
        AssertEqual(40, Marshal.SizeOf<PingResponse>());
        AssertEqual(32, Marshal.SizeOf<ReadSingleRequest>());
        AssertEqual(32, Marshal.SizeOf<WriteSingleRequestHeader>());
        AssertEqual(16, Marshal.SizeOf<WriteSingleResponse>());
    }

    private static void IoControlCodesAreStable()
    {
        AssertEqual(0x0022E000u, IoControlCodes.GetProtocolVersion);
        AssertEqual(0x0022E004u, IoControlCodes.GetCapabilities);
        AssertEqual(0x0022E008u, IoControlCodes.Ping);
        AssertEqual(0x0022E040u, IoControlCodes.ReadSingle);
        AssertEqual(0x0022E044u, IoControlCodes.WriteSingle);
        AssertEqual(0x0022E048u, IoControlCodes.ReadBatch);
        AssertEqual(0x0022E04Cu, IoControlCodes.WriteBatch);
    }

    private static void PingSerializationIsStable()
    {
        const ulong token = 0x0123456789ABCDEFUL;
        PingRequest request = PingRequest.Create(token);
        byte[] buffer = ProtocolSerializer.Serialize(in request);

        AssertEqual(24, buffer.Length);
        AssertEqual((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(0, 2)));
        AssertEqual((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(2, 2)));
        AssertEqual(24u, BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(4, 4)));
        AssertEqual(0u, BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(8, 4)));
        AssertEqual(0u, BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(12, 4)));
        AssertEqual(token, BinaryPrimitives.ReadUInt64LittleEndian(buffer.AsSpan(16, 8)));
    }

    private static void SerializationRoundTrips()
    {
        const ulong token = 0xA5A55A5AF00DBAADUL;
        PingRequest request = PingRequest.Create(token);
        PingRequest roundTrip = ProtocolSerializer.Deserialize<PingRequest>(
            ProtocolSerializer.Serialize(in request));

        AssertEqual(ProtocolConstants.ProtocolMajor, roundTrip.Header.ProtocolVersion.Major);
        AssertEqual(ProtocolConstants.ProtocolMinor, roundTrip.Header.ProtocolVersion.Minor);
        AssertEqual(24u, roundTrip.Header.StructureSize);
        AssertEqual(0u, roundTrip.Header.Flags);
        AssertEqual(0u, roundTrip.Header.Reserved);
        AssertEqual(token, roundTrip.Token);
    }

    private static void DeserializeRejectsWrongSize()
    {
        AssertThrows<ArgumentException>(() =>
            ProtocolSerializer.Deserialize<PingRequest>(new byte[23]));
        AssertThrows<ArgumentException>(() =>
            ProtocolSerializer.Deserialize<PingRequest>(new byte[25]));
    }

    private static void LimitsAndCapabilitiesAreSafe()
    {
        AssertEqual(4_096u, ProtocolConstants.MaxSingleItemSize);
        AssertEqual(128u, ProtocolConstants.MaxBatchItems);
        AssertEqual(
            524_288u,
            checked(ProtocolConstants.MaxSingleItemSize * ProtocolConstants.MaxBatchItems));
        AssertEqual(524_288u, ProtocolConstants.MaxBatchPayloadSize);

        ProtocolCapabilities expected =
            ProtocolCapabilities.GetProtocolVersion |
            ProtocolCapabilities.GetCapabilities |
            ProtocolCapabilities.Ping;

        AssertEqual(expected, ProtocolConstants.Phase02Capabilities);
        AssertEqual(
            ProtocolCapabilities.None,
            ProtocolConstants.Phase02Capabilities &
            (ProtocolCapabilities.ReadSingle |
             ProtocolCapabilities.WriteSingle |
             ProtocolCapabilities.ReadBatch |
             ProtocolCapabilities.WriteBatch));

        ProtocolCapabilities phase04Expected =
            ProtocolConstants.Phase02Capabilities |
            ProtocolCapabilities.ReadSingle |
            ProtocolCapabilities.WriteSingle;

        AssertEqual(phase04Expected, ProtocolConstants.Phase04Capabilities);
        AssertEqual(0x0000000000000307UL, (ulong)ProtocolConstants.Phase04Capabilities);
        AssertEqual(
            ProtocolCapabilities.None,
            ProtocolConstants.Phase04Capabilities &
            (ProtocolCapabilities.ReadBatch | ProtocolCapabilities.WriteBatch));
    }

    private static void SingleRequestWireFormatIsStable()
    {
        const uint processId = 1234;
        const ulong address = 0x0000012345678000UL;
        byte[] payload = [0x11, 0x22, 0x33, 0x44];

        ReadSingleRequest readRequest = ReadSingleRequest.Create(processId, address, 4);
        byte[] readBuffer = ProtocolSerializer.Serialize(in readRequest);
        AssertEqual(32, readBuffer.Length);
        AssertEqual(32u, BinaryPrimitives.ReadUInt32LittleEndian(readBuffer.AsSpan(4, 4)));
        AssertEqual(processId, BinaryPrimitives.ReadUInt32LittleEndian(readBuffer.AsSpan(16, 4)));
        AssertEqual(4u, BinaryPrimitives.ReadUInt32LittleEndian(readBuffer.AsSpan(20, 4)));
        AssertEqual(address, BinaryPrimitives.ReadUInt64LittleEndian(readBuffer.AsSpan(24, 8)));

        byte[] writeBuffer = SingleMemoryProtocol.EncodeWriteRequest(processId, address, payload);
        AssertEqual(36, writeBuffer.Length);
        AssertEqual(36u, BinaryPrimitives.ReadUInt32LittleEndian(writeBuffer.AsSpan(4, 4)));
        AssertEqual(processId, BinaryPrimitives.ReadUInt32LittleEndian(writeBuffer.AsSpan(16, 4)));
        AssertEqual(4u, BinaryPrimitives.ReadUInt32LittleEndian(writeBuffer.AsSpan(20, 4)));
        AssertEqual(address, BinaryPrimitives.ReadUInt64LittleEndian(writeBuffer.AsSpan(24, 8)));
        AssertSequenceEqual(payload, writeBuffer.AsSpan(32));

        WriteSingleMessage decoded = SingleMemoryProtocol.DecodeWriteRequest(writeBuffer);
        AssertEqual(processId, decoded.Header.TargetProcessId);
        AssertEqual(address, decoded.Header.Address);
        AssertSequenceEqual(payload, decoded.Data.Span);
    }

    private static void SingleResponseRoundTrips()
    {
        byte[] data = [0x64, 0x00, 0x00, 0x00];
        CommonResponseHeader header = new()
        {
            ProtocolVersion = ProtocolConstants.CurrentProtocolVersion,
            OperationStatus = OperationStatus.Success,
            BytesProcessed = checked((uint)data.Length),
            DetailStatus = 0,
        };

        byte[] encoded = SingleMemoryProtocol.EncodeReadResponse(header, data);
        ReadSingleMessage decoded = SingleMemoryProtocol.DecodeReadResponse(encoded);
        AssertEqual(OperationStatus.Success, decoded.Header.OperationStatus);
        AssertEqual(4u, decoded.Header.BytesProcessed);
        AssertSequenceEqual(data, decoded.Data.Span);

        AssertThrows<InvalidDataException>(() =>
            SingleMemoryProtocol.DecodeReadResponse(encoded.AsSpan(0, encoded.Length - 1)));

        byte[] malformedWrite = SingleMemoryProtocol.EncodeWriteRequest(
            1234,
            0x0000012345678000UL,
            data);
        BinaryPrimitives.WriteUInt32LittleEndian(malformedWrite.AsSpan(20, 4), 3u);
        AssertThrows<InvalidDataException>(() =>
            SingleMemoryProtocol.DecodeWriteRequest(malformedWrite));
    }

    private static void SingleRequestValidationRejectsUnsafeRanges()
    {
        const uint validPid = 1234;
        const ulong validAddress = 0x0000012345678000UL;

        AssertEqual(OperationStatus.InvalidPid,
            SingleMemoryRequestValidator.Validate(0, validAddress, 4));
        AssertEqual(OperationStatus.InvalidPid,
            SingleMemoryRequestValidator.Validate(4, validAddress, 4));
        AssertEqual(OperationStatus.InvalidAddress,
            SingleMemoryRequestValidator.Validate(validPid, 0, 4));
        AssertEqual(OperationStatus.InvalidSize,
            SingleMemoryRequestValidator.Validate(validPid, validAddress, 0));
        AssertEqual(OperationStatus.InvalidSize,
            SingleMemoryRequestValidator.Validate(validPid, validAddress, 4_097));
        AssertEqual(OperationStatus.AddressRangeOverflow,
            SingleMemoryRequestValidator.Validate(validPid, ulong.MaxValue - 1, 2));
        AssertEqual(OperationStatus.KernelRangeDenied,
            SingleMemoryRequestValidator.Validate(
                validPid,
                SingleMemoryRequestValidator.MinimumX64KernelAddress,
                4));
        AssertEqual(OperationStatus.InvalidAddress,
            SingleMemoryRequestValidator.Validate(
                validPid,
                SingleMemoryRequestValidator.MaximumX64UserAddress + 1,
                1));
        AssertEqual(OperationStatus.InvalidAddress,
            SingleMemoryRequestValidator.Validate(
                validPid,
                SingleMemoryRequestValidator.MaximumX64UserAddress,
                2));
        AssertEqual(OperationStatus.Success,
            SingleMemoryRequestValidator.Validate(validPid, validAddress, 4));
        AssertEqual(OperationStatus.Success,
            SingleMemoryRequestValidator.Validate(
                validPid,
                SingleMemoryRequestValidator.MaximumX64UserAddress,
                1));
    }

    private static void SingleOperationStatusesAreStable()
    {
        AssertEqual(1u, (uint)OperationStatus.ProtocolMismatch);
        AssertEqual(8u, (uint)OperationStatus.InvalidRequest);
        AssertEqual(9u, (uint)OperationStatus.InvalidPid);
        AssertEqual(10u, (uint)OperationStatus.TargetNotFound);
        AssertEqual(11u, (uint)OperationStatus.TargetNotAllowed);
        AssertEqual(12u, (uint)OperationStatus.InvalidAddress);
        AssertEqual(13u, (uint)OperationStatus.InvalidSize);
        AssertEqual(14u, (uint)OperationStatus.AddressRangeOverflow);
        AssertEqual(15u, (uint)OperationStatus.KernelRangeDenied);
        AssertEqual(16u, (uint)OperationStatus.MemoryNotAccessible);
        AssertEqual(17u, (uint)OperationStatus.PartialTransfer);
        AssertEqual(18u, (uint)OperationStatus.TargetExited);
    }

    private static void AssertEqual<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
        {
            throw new InvalidOperationException($"Expected {expected}, actual {actual}.");
        }
    }

    private static void AssertSequenceEqual(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual)
    {
        if (!expected.SequenceEqual(actual))
        {
            throw new InvalidOperationException(
                $"Expected {Convert.ToHexString(expected)}, actual {Convert.ToHexString(actual)}.");
        }
    }

    private static void AssertThrows<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"Expected exception {typeof(TException).Name}.");
    }
}
