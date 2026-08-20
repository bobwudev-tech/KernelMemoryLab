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
        (nameof(BatchStructureSizesAreStable), BatchStructureSizesAreStable),
        (nameof(BatchReadSerializationIsStable), BatchReadSerializationIsStable),
        (nameof(BatchWriteSerializationIsStable), BatchWriteSerializationIsStable),
        (nameof(BatchMalformedOffsetsAreRejected), BatchMalformedOffsetsAreRejected),
        (nameof(BatchLimitsAreEnforced), BatchLimitsAreEnforced),
        (nameof(BatchResponsesRoundTrip), BatchResponsesRoundTrip),
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

        ProtocolCapabilities phase05Expected =
            ProtocolConstants.Phase04Capabilities |
            ProtocolCapabilities.ReadBatch |
            ProtocolCapabilities.WriteBatch;

        AssertEqual(phase05Expected, ProtocolConstants.Phase05Capabilities);
        AssertEqual(0x0000000000000F07UL, (ulong)ProtocolConstants.Phase05Capabilities);
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
        AssertEqual(19u, (uint)OperationStatus.InvalidItemCount);
        AssertEqual(20u, (uint)OperationStatus.InvalidOffset);
        AssertEqual(21u, (uint)OperationStatus.AggregateLimitExceeded);
        AssertEqual(22u, (uint)OperationStatus.AllItemsFailed);
    }

    private static void BatchStructureSizesAreStable()
    {
        AssertEqual(32, Marshal.SizeOf<ReadBatchRequestHeader>());
        AssertEqual(16, Marshal.SizeOf<ReadBatchItem>());
        AssertEqual(32, Marshal.SizeOf<ReadBatchResponseHeader>());
        AssertEqual(40, Marshal.SizeOf<WriteBatchRequestHeader>());
        AssertEqual(16, Marshal.SizeOf<WriteBatchItem>());
        AssertEqual(24, Marshal.SizeOf<WriteBatchResponseHeader>());
        AssertEqual(24, Marshal.SizeOf<BatchItemResult>());
    }

    private static void BatchReadSerializationIsStable()
    {
        const uint processId = 1234;
        BatchReadRequestItem[] items =
        [
            new(0x0000010000001000UL, 4),
            new(0x0000010000002000UL, 4),
            new(0x0000010000003000UL, 8),
            new(0x0000010000004000UL, 4),
            new(0x0000010000005000UL, 4),
        ];

        byte[] buffer = BatchMemoryProtocol.EncodeReadRequest(processId, items);
        AssertEqual(112, buffer.Length);
        AssertEqual(112u, BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(4, 4)));
        AssertEqual(processId, BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(16, 4)));
        AssertEqual(5u, BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(20, 4)));
        AssertEqual(32u, BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(24, 4)));
        AssertEqual(0u, BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(28, 4)));

        AssertEqual(items[0].Address, BinaryPrimitives.ReadUInt64LittleEndian(buffer.AsSpan(32, 8)));
        AssertEqual(4u, BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(40, 4)));
        AssertEqual(0u, BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(44, 4)));
        AssertEqual(8u, BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(76, 4)));
        AssertEqual(16u, BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(92, 4)));
        AssertEqual(20u, BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(108, 4)));
        AssertEqual(OperationStatus.Success, BatchMemoryProtocol.ValidateReadRequest(buffer));
    }

    private static void BatchWriteSerializationIsStable()
    {
        const uint processId = 1234;
        BatchWriteRequestItem[] items =
        [
            new(0x0000010000001000UL, new byte[] { 0x09, 0x03, 0x00, 0x00 }),
            new(0x0000010000002000UL, new byte[] { 0x4B, 0x00, 0x00, 0x00 }),
            new(0x0000010000003000UL, new byte[] { 0xD2, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }),
        ];

        byte[] buffer = BatchMemoryProtocol.EncodeWriteRequest(processId, items);
        AssertEqual(104, buffer.Length);
        AssertEqual(104u, BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(4, 4)));
        AssertEqual(3u, BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(20, 4)));
        AssertEqual(40u, BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(24, 4)));
        AssertEqual(88u, BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(28, 4)));
        AssertEqual(16u, BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(32, 4)));
        AssertEqual(88u, BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(52, 4)));
        AssertEqual(92u, BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(68, 4)));
        AssertEqual(96u, BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(84, 4)));
        AssertSequenceEqual(items[0].Data.Span, buffer.AsSpan(88, 4));
        AssertSequenceEqual(items[2].Data.Span, buffer.AsSpan(96, 8));
        AssertEqual(OperationStatus.Success, BatchMemoryProtocol.ValidateWriteRequest(buffer));
    }

    private static void BatchMalformedOffsetsAreRejected()
    {
        BatchReadRequestItem[] readItems =
        [
            new(0x0000010000001000UL, 4),
            new(0x0000010000002000UL, 4),
        ];
        byte[] readBuffer = BatchMemoryProtocol.EncodeReadRequest(1234, readItems);
        BinaryPrimitives.WriteUInt32LittleEndian(readBuffer.AsSpan(44, 4), 1u);
        AssertEqual(
            OperationStatus.InvalidOffset,
            BatchMemoryProtocol.ValidateReadRequest(readBuffer));

        BatchWriteRequestItem[] writeItems =
        [
            new(0x0000010000001000UL, new byte[4]),
            new(0x0000010000002000UL, new byte[4]),
        ];
        byte[] writeBuffer = BatchMemoryProtocol.EncodeWriteRequest(1234, writeItems);
        BinaryPrimitives.WriteUInt32LittleEndian(writeBuffer.AsSpan(68, 4), uint.MaxValue);
        AssertEqual(
            OperationStatus.InvalidOffset,
            BatchMemoryProtocol.ValidateWriteRequest(writeBuffer));

        byte[] overflowBuffer = BatchMemoryProtocol.EncodeWriteRequest(1234, writeItems);
        BinaryPrimitives.WriteUInt32LittleEndian(overflowBuffer.AsSpan(28, 4), uint.MaxValue);
        AssertEqual(
            OperationStatus.InvalidOffset,
            BatchMemoryProtocol.ValidateWriteRequest(overflowBuffer));
    }

    private static void BatchLimitsAreEnforced()
    {
        BatchReadRequestItem[] maximumItems = Enumerable.Range(
                0,
                checked((int)ProtocolConstants.MaxBatchItems))
            .Select(index => new BatchReadRequestItem(
                checked(0x0000010000000000UL + ((ulong)index * 0x2000UL)),
                ProtocolConstants.MaxSingleItemSize))
            .ToArray();

        byte[] maximumBuffer = BatchMemoryProtocol.EncodeReadRequest(1234, maximumItems);
        AssertEqual(OperationStatus.Success, BatchMemoryProtocol.ValidateReadRequest(maximumBuffer));

        BatchReadRequestItem[] tooManyItems =
            new BatchReadRequestItem[ProtocolConstants.MaxBatchItems + 1];
        AssertThrows<ArgumentOutOfRangeException>(() =>
            BatchMemoryProtocol.EncodeReadRequest(1234, tooManyItems));

        AssertThrows<ArgumentOutOfRangeException>(() =>
            BatchMemoryProtocol.EncodeReadRequest(
                1234,
                [new BatchReadRequestItem(0x0000010000001000UL, 0)]));

        byte[] countBuffer = BatchMemoryProtocol.EncodeReadRequest(
            1234,
            [new BatchReadRequestItem(0x0000010000001000UL, 4)]);
        BinaryPrimitives.WriteUInt32LittleEndian(
            countBuffer.AsSpan(20, 4),
            ProtocolConstants.MaxBatchItems + 1);
        AssertEqual(
            OperationStatus.InvalidItemCount,
            BatchMemoryProtocol.ValidateReadRequest(countBuffer));

        byte[] aggregateBuffer = BatchMemoryProtocol.EncodeWriteRequest(
            1234,
            [new BatchWriteRequestItem(0x0000010000001000UL, new byte[4])]);
        BinaryPrimitives.WriteUInt32LittleEndian(
            aggregateBuffer.AsSpan(32, 4),
            ProtocolConstants.MaxBatchPayloadSize + 1);
        AssertEqual(
            OperationStatus.AggregateLimitExceeded,
            BatchMemoryProtocol.ValidateWriteRequest(aggregateBuffer));
    }

    private static void BatchResponsesRoundTrip()
    {
        byte[] data = [100, 0, 0, 0, 0, 0, 0, 0];
        BatchItemResult[] readResults =
        [
            new()
            {
                OperationStatus = OperationStatus.Success,
                BytesProcessed = 4,
                DataOffset = 80,
                RequestedSize = 4,
                DetailStatus = 0,
                Reserved = 0,
            },
            new()
            {
                OperationStatus = OperationStatus.InvalidAddress,
                BytesProcessed = 0,
                DataOffset = 84,
                RequestedSize = 4,
                DetailStatus = 0xC000000D,
                Reserved = 0,
            },
        ];
        ReadBatchResponseHeader readHeader = new()
        {
            Header = new CommonResponseHeader
            {
                ProtocolVersion = ProtocolConstants.CurrentProtocolVersion,
                OperationStatus = OperationStatus.PartialTransfer,
                BytesProcessed = 4,
                DetailStatus = 0x8000000D,
            },
            ItemCount = 2,
            ResultsOffset = 32,
            DataOffset = 80,
            DataSize = 8,
        };

        byte[] readBuffer = BatchMemoryProtocol.EncodeReadResponse(readHeader, readResults, data);
        BatchReadResponseMessage readResponse = BatchMemoryProtocol.DecodeReadResponse(readBuffer);
        AssertEqual(OperationStatus.PartialTransfer, readResponse.Header.Header.OperationStatus);
        AssertEqual(OperationStatus.Success, readResponse.Results[0].OperationStatus);
        AssertEqual(OperationStatus.InvalidAddress, readResponse.Results[1].OperationStatus);
        AssertSequenceEqual(data, readResponse.Data.Span);

        BatchItemResult[] writeResults =
        [
            readResults[0],
            readResults[1] with { DataOffset = 0 },
        ];
        writeResults[0].DataOffset = 0;
        WriteBatchResponseHeader writeHeader = new()
        {
            Header = readHeader.Header,
            ItemCount = 2,
            ResultsOffset = 24,
        };

        byte[] writeBuffer = BatchMemoryProtocol.EncodeWriteResponse(writeHeader, writeResults);
        BatchWriteResponseMessage writeResponse = BatchMemoryProtocol.DecodeWriteResponse(writeBuffer);
        AssertEqual(2, writeResponse.Results.Count);
        AssertEqual(OperationStatus.InvalidAddress, writeResponse.Results[1].OperationStatus);
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
