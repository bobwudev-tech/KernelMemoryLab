using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using KernelMemoryLab.Protocol;

namespace KernelMemoryLab.Controller.DriverApi;

public sealed class KernelMemoryApi : IDisposable
{
    private readonly IDriverTransport _transport;

    public KernelMemoryApi()
        : this(new Win32DriverTransport())
    {
    }

    public KernelMemoryApi(IDriverTransport transport)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    public bool IsOpen => _transport.IsOpen;

    public void Open()
    {
        try
        {
            _transport.Open();
        }
        catch (Win32Exception exception)
        {
            throw CreateTransportException("Open", null, exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new DriverApiException("Open", exception.Message, innerException: exception);
        }
    }

    public void Close() => _transport.Close();

    public GetProtocolVersionResponse GetProtocolVersion()
    {
        GetProtocolVersionRequest request = GetProtocolVersionRequest.Create();
        GetProtocolVersionResponse response = InvokeFixed<
            GetProtocolVersionRequest,
            GetProtocolVersionResponse>(
                "GetProtocolVersion",
                IoControlCodes.GetProtocolVersion,
                request,
                null);
        EnsureSuccess("GetProtocolVersion", response.Header, null);
        return response;
    }

    public GetCapabilitiesResponse GetCapabilities()
    {
        GetCapabilitiesRequest request = GetCapabilitiesRequest.Create();
        GetCapabilitiesResponse response = InvokeFixed<
            GetCapabilitiesRequest,
            GetCapabilitiesResponse>(
                "GetCapabilities",
                IoControlCodes.GetCapabilities,
                request,
                null);
        EnsureSuccess("GetCapabilities", response.Header, null);
        return response;
    }

    public PingResponse Ping(ulong token = 0x0123456789ABCDEFUL)
    {
        PingRequest request = PingRequest.Create(token);
        PingResponse response = InvokeFixed<PingRequest, PingResponse>(
            "Ping",
            IoControlCodes.Ping,
            request,
            null);
        EnsureSuccess("Ping", response.Header, null);
        if (response.EchoToken != token)
        {
            throw new DriverApiException("Ping", "Driver returned an unexpected echo token.");
        }

        return response;
    }

    public ReadSingleMessage Read(uint targetProcessId, ulong address, uint size)
    {
        ValidateMemoryRequest("Read", targetProcessId, address, size);
        ReadSingleRequest request = ReadSingleRequest.Create(targetProcessId, address, size);
        byte[] input = ProtocolSerializer.Serialize(in request);
        byte[] output = InvokeTransport(
            "Read",
            IoControlCodes.ReadSingle,
            input,
            checked(SingleMemoryProtocol.ResponseHeaderSize + (int)size),
            targetProcessId);
        ReadSingleMessage response = SingleMemoryProtocol.DecodeReadResponse(output);
        EnsureSuccess("Read", response.Header, targetProcessId);
        return response;
    }

    public WriteSingleResponse Write(
        uint targetProcessId,
        ulong address,
        ReadOnlySpan<byte> data)
    {
        ValidateMemoryRequest(
            "Write",
            targetProcessId,
            address,
            checked((uint)data.Length));
        byte[] input = SingleMemoryProtocol.EncodeWriteRequest(targetProcessId, address, data);
        byte[] output = InvokeTransport(
            "Write",
            IoControlCodes.WriteSingle,
            input,
            Marshal.SizeOf<WriteSingleResponse>(),
            targetProcessId);
        WriteSingleResponse response = DeserializeExact<WriteSingleResponse>("Write", output, targetProcessId);
        EnsureSuccess("Write", response.Header, targetProcessId);
        return response;
    }

    public BatchReadResponseMessage ReadBatch(
        uint targetProcessId,
        IReadOnlyList<BatchReadRequestItem> requests)
    {
        ValidateReadBatch(targetProcessId, requests);
        byte[] input = BatchMemoryProtocol.EncodeReadRequest(targetProcessId, requests);
        int aggregateSize = requests.Sum(item => checked((int)item.Size));
        int outputCapacity = checked(
            BatchMemoryProtocol.ReadResponseHeaderSize +
            (BatchMemoryProtocol.ItemResultSize * requests.Count) +
            aggregateSize);
        byte[] output = InvokeTransport(
            "ReadBatch",
            IoControlCodes.ReadBatch,
            input,
            outputCapacity,
            targetProcessId);
        EnsureBatchEnvelopeStatus("ReadBatch", output, targetProcessId);
        return BatchMemoryProtocol.DecodeReadResponse(output);
    }

    public BatchWriteResponseMessage WriteBatch(
        uint targetProcessId,
        IReadOnlyList<BatchWriteRequestItem> requests)
    {
        ValidateWriteBatch(targetProcessId, requests);
        byte[] input = BatchMemoryProtocol.EncodeWriteRequest(targetProcessId, requests);
        int outputCapacity = checked(
            BatchMemoryProtocol.WriteResponseHeaderSize +
            (BatchMemoryProtocol.ItemResultSize * requests.Count));
        byte[] output = InvokeTransport(
            "WriteBatch",
            IoControlCodes.WriteBatch,
            input,
            outputCapacity,
            targetProcessId);
        EnsureBatchEnvelopeStatus("WriteBatch", output, targetProcessId);
        return BatchMemoryProtocol.DecodeWriteResponse(output);
    }

    public int ReadInt32(uint targetProcessId, ulong address) =>
        BinaryPrimitives.ReadInt32LittleEndian(
            ReadExact(targetProcessId, address, sizeof(int), "ReadInt32"));

    public long ReadInt64(uint targetProcessId, ulong address) =>
        BinaryPrimitives.ReadInt64LittleEndian(
            ReadExact(targetProcessId, address, sizeof(long), "ReadInt64"));

    public float ReadFloat32(uint targetProcessId, ulong address) =>
        BitConverter.Int32BitsToSingle(
            BinaryPrimitives.ReadInt32LittleEndian(
                ReadExact(targetProcessId, address, sizeof(float), "ReadFloat32")));

    public WriteSingleResponse WriteInt32(uint targetProcessId, ulong address, int value)
    {
        Span<byte> data = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(data, value);
        return Write(targetProcessId, address, data);
    }

    public WriteSingleResponse WriteInt64(uint targetProcessId, ulong address, long value)
    {
        Span<byte> data = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(data, value);
        return Write(targetProcessId, address, data);
    }

    public WriteSingleResponse WriteFloat32(uint targetProcessId, ulong address, float value)
    {
        Span<byte> data = stackalloc byte[sizeof(float)];
        BinaryPrimitives.WriteInt32LittleEndian(data, BitConverter.SingleToInt32Bits(value));
        return Write(targetProcessId, address, data);
    }

    public void Dispose() => _transport.Dispose();

    private static void ValidateReadBatch(
        uint targetProcessId,
        IReadOnlyList<BatchReadRequestItem> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);
        ValidateBatchCount("ReadBatch", targetProcessId, requests.Count);
        foreach (BatchReadRequestItem request in requests)
        {
            ValidateMemoryRequest("ReadBatch", targetProcessId, request.Address, request.Size);
        }
    }

    private static void ValidateWriteBatch(
        uint targetProcessId,
        IReadOnlyList<BatchWriteRequestItem> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);
        ValidateBatchCount("WriteBatch", targetProcessId, requests.Count);
        foreach (BatchWriteRequestItem request in requests)
        {
            ValidateMemoryRequest(
                "WriteBatch",
                targetProcessId,
                request.Address,
                checked((uint)request.Data.Length));
        }
    }

    private static void ValidateBatchCount(string operation, uint targetProcessId, int count)
    {
        if (count <= 0 || count > ProtocolConstants.MaxBatchItems)
        {
            throw new DriverApiException(
                operation,
                $"Batch item count must be between 1 and {ProtocolConstants.MaxBatchItems}.",
                OperationStatus.InvalidItemCount,
                targetProcessId: targetProcessId);
        }
    }

    private static void ValidateMemoryRequest(
        string operation,
        uint targetProcessId,
        ulong address,
        uint size)
    {
        OperationStatus status = SingleMemoryRequestValidator.Validate(
            targetProcessId,
            address,
            size);
        if (status != OperationStatus.Success)
        {
            throw new DriverApiException(
                operation,
                $"Controller-side validation rejected the request: {status}.",
                status,
                targetProcessId: targetProcessId);
        }
    }

    private ReadOnlySpan<byte> ReadExact(
        uint targetProcessId,
        ulong address,
        uint size,
        string operation)
    {
        ReadSingleMessage response = Read(targetProcessId, address, size);
        if (response.Data.Length != size)
        {
            throw new DriverApiException(
                operation,
                $"Expected {size} bytes, received {response.Data.Length}.",
                response.Header.OperationStatus,
                targetProcessId: targetProcessId,
                detailStatus: response.Header.DetailStatus);
        }

        return response.Data.Span;
    }

    private TResponse InvokeFixed<TRequest, TResponse>(
        string operation,
        uint ioControlCode,
        TRequest request,
        uint? targetProcessId)
        where TRequest : unmanaged
        where TResponse : unmanaged
    {
        byte[] input = ProtocolSerializer.Serialize(in request);
        byte[] output = InvokeTransport(
            operation,
            ioControlCode,
            input,
            Marshal.SizeOf<TResponse>(),
            targetProcessId);
        return DeserializeExact<TResponse>(operation, output, targetProcessId);
    }

    private byte[] InvokeTransport(
        string operation,
        uint ioControlCode,
        ReadOnlySpan<byte> input,
        int outputCapacity,
        uint? targetProcessId)
    {
        if (!IsOpen)
        {
            throw new DriverApiException(
                operation,
                "Driver device is not open.",
                targetProcessId: targetProcessId);
        }

        try
        {
            return _transport.Invoke(ioControlCode, input, outputCapacity);
        }
        catch (Win32Exception exception)
        {
            throw CreateTransportException(operation, targetProcessId, exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new DriverApiException(
                operation,
                exception.Message,
                targetProcessId: targetProcessId,
                innerException: exception);
        }
    }

    private static TResponse DeserializeExact<TResponse>(
        string operation,
        ReadOnlySpan<byte> output,
        uint? targetProcessId)
        where TResponse : unmanaged
    {
        if (output.Length != Marshal.SizeOf<TResponse>())
        {
            throw new DriverApiException(
                operation,
                $"Unexpected response length: {output.Length}.",
                targetProcessId: targetProcessId);
        }

        return ProtocolSerializer.Deserialize<TResponse>(output);
    }

    private static void EnsureBatchEnvelopeStatus(
        string operation,
        ReadOnlySpan<byte> output,
        uint targetProcessId)
    {
        if (output.Length < Marshal.SizeOf<CommonResponseHeader>())
        {
            throw new DriverApiException(
                operation,
                "Batch response is smaller than the common response header.",
                targetProcessId: targetProcessId);
        }

        CommonResponseHeader header = ProtocolSerializer.Deserialize<CommonResponseHeader>(
            output[..Marshal.SizeOf<CommonResponseHeader>()]);
        if (header.OperationStatus is not (
            OperationStatus.Success or
            OperationStatus.PartialTransfer or
            OperationStatus.AllItemsFailed))
        {
            throw FromHeader(operation, header, targetProcessId);
        }
    }

    private static void EnsureSuccess(
        string operation,
        CommonResponseHeader header,
        uint? targetProcessId)
    {
        if (header.OperationStatus != OperationStatus.Success)
        {
            throw FromHeader(operation, header, targetProcessId);
        }
    }

    private static DriverApiException FromHeader(
        string operation,
        CommonResponseHeader header,
        uint? targetProcessId) =>
        new(
            operation,
            $"Driver rejected {operation}: {header.OperationStatus}.",
            header.OperationStatus,
            targetProcessId: targetProcessId,
            detailStatus: header.DetailStatus);

    private static DriverApiException CreateTransportException(
        string operation,
        uint? targetProcessId,
        Win32Exception exception) =>
        new(
            operation,
            exception.Message,
            win32Error: exception.NativeErrorCode,
            targetProcessId: targetProcessId,
            innerException: exception);
}
