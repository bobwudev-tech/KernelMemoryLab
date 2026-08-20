using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using KernelMemoryLab.Controller.DriverApi;
using KernelMemoryLab.Controller.ViewModels;
using KernelMemoryLab.Protocol;

namespace KernelMemoryLab.Controller.Tests;

internal static class Program
{
    private static readonly (string Name, Action Test)[] Tests =
    [
        (nameof(AddressParserRejectsMalformedInput), AddressParserRejectsMalformedInput),
        (nameof(ConnectionOperationsSerializeCorrectly), ConnectionOperationsSerializeCorrectly),
        (nameof(SingleTypedOperationsUseExpectedWireFormat), SingleTypedOperationsUseExpectedWireFormat),
        (nameof(BatchOperationsUseExpectedWireFormat), BatchOperationsUseExpectedWireFormat),
        (nameof(ControllerValidationPreventsTransportCalls), ControllerValidationPreventsTransportCalls),
        (nameof(ViewModelHandlesUnavailableDriver), ViewModelHandlesUnavailableDriver),
        (nameof(ViewModelShowsStructuredDriverError), ViewModelShowsStructuredDriverError),
    ];

    private static int Main()
    {
        int failed = 0;
        foreach ((string name, Action test) in Tests)
        {
            try
            {
                test();
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception exception)
            {
                failed++;
                Console.Error.WriteLine($"FAIL {name}: {exception.Message}");
            }
        }

        Console.WriteLine($"Controller tests: {Tests.Length - failed} passed, {failed} failed.");
        return failed == 0 ? 0 : 1;
    }

    private static void AddressParserRejectsMalformedInput()
    {
        AssertEqual(0x1234UL, ControllerViewModel.ParseAddress("0x1234"));
        AssertEqual(1234UL, ControllerViewModel.ParseAddress("1234"));
        AssertThrows<FormatException>(() => ControllerViewModel.ParseAddress("not-an-address"));
        AssertThrows<FormatException>(() => ControllerViewModel.ParseAddress("0x0"));
        AssertThrows<FormatException>(() => ControllerViewModel.ParseAddress(string.Empty));
    }

    private static void ConnectionOperationsSerializeCorrectly()
    {
        FakeDriverTransport transport = new()
        {
            Responder = (code, input, _) => code switch
            {
                IoControlCodes.GetProtocolVersion => RespondProtocolVersion(input),
                IoControlCodes.GetCapabilities => RespondCapabilities(input),
                IoControlCodes.Ping => RespondPing(input),
                _ => throw new InvalidOperationException("Unexpected IOCTL."),
            },
        };
        using KernelMemoryApi api = new(transport);

        api.Open();
        GetProtocolVersionResponse protocol = api.GetProtocolVersion();
        GetCapabilitiesResponse capabilities = api.GetCapabilities();
        PingResponse ping = api.Ping(0x8877665544332211UL);

        AssertTrue(api.IsOpen);
        AssertEqual(ProtocolConstants.ProtocolMajor, protocol.Header.ProtocolVersion.Major);
        AssertEqual(ProtocolConstants.Phase05Capabilities, capabilities.Capabilities);
        AssertEqual(0x8877665544332211UL, ping.EchoToken);
        AssertEqual(3, transport.Invocations.Count);
    }

    private static void SingleTypedOperationsUseExpectedWireFormat()
    {
        FakeDriverTransport transport = new();
        transport.Responder = (code, input, _) =>
        {
            if (code == IoControlCodes.ReadSingle)
            {
                ReadSingleRequest request = ProtocolSerializer.Deserialize<ReadSingleRequest>(input);
                AssertEqual(1234u, request.TargetProcessId);
                AssertEqual(0x1000UL, request.Address);
                AssertEqual(4u, request.Size);
                byte[] data = new byte[sizeof(int)];
                BinaryPrimitives.WriteInt32LittleEndian(data, 777);
                return SingleMemoryProtocol.EncodeReadResponse(SuccessHeader(sizeof(int)), data);
            }

            if (code == IoControlCodes.WriteSingle)
            {
                WriteSingleMessage request = SingleMemoryProtocol.DecodeWriteRequest(input);
                AssertEqual(1234u, request.Header.TargetProcessId);
                AssertEqual(0x2000UL, request.Header.Address);
                AssertEqual(12.5f, BitConverter.Int32BitsToSingle(
                    BinaryPrimitives.ReadInt32LittleEndian(request.Data.Span)));
                WriteSingleResponse response = new() { Header = SuccessHeader(sizeof(float)) };
                return ProtocolSerializer.Serialize(in response);
            }

            throw new InvalidOperationException("Unexpected IOCTL.");
        };

        using KernelMemoryApi api = new(transport);
        api.Open();
        AssertEqual(777, api.ReadInt32(1234, 0x1000));
        WriteSingleResponse write = api.WriteFloat32(1234, 0x2000, 12.5f);
        AssertEqual(OperationStatus.Success, write.Header.OperationStatus);
        AssertEqual(2, transport.Invocations.Count);
    }

    private static void BatchOperationsUseExpectedWireFormat()
    {
        FakeDriverTransport transport = new();
        transport.Responder = (code, input, _) =>
        {
            if (code == IoControlCodes.ReadBatch)
            {
                AssertEqual(OperationStatus.Success, BatchMemoryProtocol.ValidateReadRequest(input));
                byte[] data = new byte[12];
                BinaryPrimitives.WriteInt32LittleEndian(data, 100);
                BinaryPrimitives.WriteInt64LittleEndian(data.AsSpan(4), 1000);
                BatchItemResult[] results =
                [
                    SuccessItem(sizeof(int), 80),
                    SuccessItem(sizeof(long), 84),
                ];
                ReadBatchResponseHeader header = new()
                {
                    Header = SuccessHeader(12),
                    ItemCount = 2,
                    ResultsOffset = 32,
                    DataOffset = 80,
                    DataSize = 12,
                };
                return BatchMemoryProtocol.EncodeReadResponse(header, results, data);
            }

            if (code == IoControlCodes.WriteBatch)
            {
                AssertEqual(OperationStatus.Success, BatchMemoryProtocol.ValidateWriteRequest(input));
                BatchItemResult[] results =
                [
                    SuccessItem(sizeof(int), 0),
                    SuccessItem(sizeof(long), 0),
                ];
                WriteBatchResponseHeader header = new()
                {
                    Header = SuccessHeader(12),
                    ItemCount = 2,
                    ResultsOffset = 24,
                };
                return BatchMemoryProtocol.EncodeWriteResponse(header, results);
            }

            throw new InvalidOperationException("Unexpected IOCTL.");
        };

        using KernelMemoryApi api = new(transport);
        api.Open();
        BatchReadResponseMessage read = api.ReadBatch(
            1234,
            [new(0x1000, sizeof(int)), new(0x2000, sizeof(long))]);
        BatchWriteResponseMessage write = api.WriteBatch(
            1234,
            [
                new(0x1000, EncodeInt32(777)),
                new(0x2000, EncodeInt64(1234)),
            ]);

        AssertEqual(12u, read.Header.Header.BytesProcessed);
        AssertEqual(2, read.Results.Count);
        AssertEqual(OperationStatus.Success, write.Header.Header.OperationStatus);
        AssertEqual(2, transport.Invocations.Count);
    }

    private static void ControllerValidationPreventsTransportCalls()
    {
        FakeDriverTransport transport = new();
        using KernelMemoryApi api = new(transport);
        api.Open();

        AssertThrows<DriverApiException>(() => api.Read(1234, 0, sizeof(int)));
        AssertThrows<DriverApiException>(() => api.Read(1234, 0x1000, 0));
        AssertThrows<DriverApiException>(() => api.Read(
            1234,
            0x1000,
            ProtocolConstants.MaxSingleItemSize + 1));

        BatchReadRequestItem[] tooMany = Enumerable
            .Range(0, checked((int)ProtocolConstants.MaxBatchItems + 1))
            .Select(index => new BatchReadRequestItem(checked((ulong)0x1000 + (uint)index), 1))
            .ToArray();
        AssertThrows<DriverApiException>(() => api.ReadBatch(1234, tooMany));
        AssertEqual(0, transport.Invocations.Count);
    }

    private static void ViewModelHandlesUnavailableDriver()
    {
        FakeDriverTransport transport = new()
        {
            OpenException = new Win32Exception(2, "Mock device unavailable."),
        };
        using KernelMemoryApi api = new(transport);
        using ControllerViewModel viewModel = new(api);

        viewModel.Connect();

        AssertFalse(viewModel.IsConnected);
        AssertEqual("Driver Disconnected", viewModel.ConnectionStatus);
        AssertContains("Operation=Open", viewModel.LastOperation);
        AssertContains("Win32Error=2", viewModel.LastOperation);
    }

    private static void ViewModelShowsStructuredDriverError()
    {
        FakeDriverTransport transport = new()
        {
            Responder = (code, _, _) =>
            {
                AssertEqual(IoControlCodes.ReadSingle, code);
                CommonResponseHeader header = new()
                {
                    ProtocolVersion = ProtocolConstants.CurrentProtocolVersion,
                    OperationStatus = OperationStatus.InvalidAddress,
                    BytesProcessed = 0,
                    DetailStatus = 0xC0000005,
                };
                return SingleMemoryProtocol.EncodeReadResponse(header, []);
            },
        };
        using KernelMemoryApi api = new(transport);
        api.Open();
        using ControllerViewModel viewModel = new(api)
        {
            ProcessId = "1234",
            SingleAddress = "0x1000",
            SelectedType = "Int32",
        };

        viewModel.ReadSingle();

        AssertEqual("Error", viewModel.ReadResult);
        AssertContains("Operation=Read", viewModel.LastOperation);
        AssertContains("DriverStatus=InvalidAddress", viewModel.LastOperation);
        AssertContains("Win32Error=N/A", viewModel.LastOperation);
        AssertContains("TargetPID=1234", viewModel.LastOperation);
    }

    private static byte[] RespondProtocolVersion(byte[] input)
    {
        _ = ProtocolSerializer.Deserialize<GetProtocolVersionRequest>(input);
        GetProtocolVersionResponse response = new() { Header = SuccessHeader(0) };
        return ProtocolSerializer.Serialize(in response);
    }

    private static byte[] RespondCapabilities(byte[] input)
    {
        _ = ProtocolSerializer.Deserialize<GetCapabilitiesRequest>(input);
        GetCapabilitiesResponse response = new()
        {
            Header = SuccessHeader(0),
            Capabilities = ProtocolConstants.Phase05Capabilities,
            MaxSingleItemSize = ProtocolConstants.MaxSingleItemSize,
            MaxBatchItems = ProtocolConstants.MaxBatchItems,
            MaxBatchPayloadSize = ProtocolConstants.MaxBatchPayloadSize,
            Reserved = 0,
        };
        return ProtocolSerializer.Serialize(in response);
    }

    private static byte[] RespondPing(byte[] input)
    {
        PingRequest request = ProtocolSerializer.Deserialize<PingRequest>(input);
        PingResponse response = new()
        {
            Header = SuccessHeader(0),
            DriverVersion = new DriverVersion(0, 5, 0, 0),
            Capabilities = ProtocolConstants.Phase05Capabilities,
            EchoToken = request.Token,
        };
        return ProtocolSerializer.Serialize(in response);
    }

    private static CommonResponseHeader SuccessHeader(int bytesProcessed) =>
        new()
        {
            ProtocolVersion = ProtocolConstants.CurrentProtocolVersion,
            OperationStatus = OperationStatus.Success,
            BytesProcessed = checked((uint)bytesProcessed),
            DetailStatus = 0,
        };

    private static BatchItemResult SuccessItem(int size, uint dataOffset) =>
        new()
        {
            OperationStatus = OperationStatus.Success,
            BytesProcessed = checked((uint)size),
            DataOffset = dataOffset,
            RequestedSize = checked((uint)size),
            DetailStatus = 0,
            Reserved = 0,
        };

    private static byte[] EncodeInt32(int value)
    {
        byte[] data = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(data, value);
        return data;
    }

    private static byte[] EncodeInt64(long value)
    {
        byte[] data = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(data, value);
        return data;
    }

    private static void AssertTrue(bool condition)
    {
        if (!condition)
        {
            throw new InvalidOperationException("Expected condition to be true.");
        }
    }

    private static void AssertFalse(bool condition) => AssertTrue(!condition);

    private static void AssertEqual<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected {expected}; received {actual}.");
        }
    }

    private static void AssertContains(string expected, string actual)
    {
        if (!actual.Contains(expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected '{actual}' to contain '{expected}'.");
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

        throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
    }

    private sealed class FakeDriverTransport : IDriverTransport
    {
        public bool IsOpen { get; private set; }

        public Exception? OpenException { get; init; }

        public Func<uint, byte[], int, byte[]>? Responder { get; set; }

        public List<Invocation> Invocations { get; } = [];

        public void Open()
        {
            if (OpenException is not null)
            {
                throw OpenException;
            }

            IsOpen = true;
        }

        public void Close() => IsOpen = false;

        public byte[] Invoke(uint ioControlCode, ReadOnlySpan<byte> input, int outputCapacity)
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException("Fake transport is closed.");
            }

            byte[] inputCopy = input.ToArray();
            Invocations.Add(new Invocation(ioControlCode, inputCopy, outputCapacity));
            return Responder?.Invoke(ioControlCode, inputCopy, outputCapacity) ??
                throw new InvalidOperationException("No fake response configured.");
        }

        public void Dispose() => Close();
    }

    private sealed record Invocation(uint IoControlCode, byte[] Input, int OutputCapacity);
}
