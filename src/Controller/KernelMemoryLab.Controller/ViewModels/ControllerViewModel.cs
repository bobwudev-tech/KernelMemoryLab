using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using KernelMemoryLab.Controller.DriverApi;
using KernelMemoryLab.Protocol;

namespace KernelMemoryLab.Controller.ViewModels;

public sealed class ControllerViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly KernelMemoryApi _api;
    private readonly string _targetProcessName = "KernelMemoryLab.Target.exe";
    private string _connectionStatus = "Driver Disconnected";
    private string _protocolVersion = "—";
    private string _driverVersion = "—";
    private string _capabilities = "—";
    private string _processId = string.Empty;
    private string _singleAddress = string.Empty;
    private string _selectedType = "Int32";
    private string _readResult = string.Empty;
    private string _writeValue = string.Empty;
    private string _writeResult = string.Empty;
    private string _lastOperation = "No operation has been run.";

    public ControllerViewModel()
        : this(new KernelMemoryApi())
    {
    }

    public ControllerViewModel(KernelMemoryApi api)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        BatchRows =
        [
            new() { Name = "Health", Type = "Int32" },
            new() { Name = "Mana", Type = "Int32" },
            new() { Name = "Gold", Type = "Int64" },
            new() { Name = "PositionX", Type = "Float32" },
            new() { Name = "PositionY", Type = "Float32" },
        ];
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static IReadOnlyList<string> SupportedTypes { get; } = ["Int32", "Int64", "Float32"];

    public string TargetProcessName => _targetProcessName;

    public ObservableCollection<BatchMemoryRowViewModel> BatchRows { get; }

    public bool IsConnected => _api.IsOpen;

    public string ConnectionStatus { get => _connectionStatus; private set => SetField(ref _connectionStatus, value); }

    public string ProtocolVersion { get => _protocolVersion; private set => SetField(ref _protocolVersion, value); }

    public string DriverVersion { get => _driverVersion; private set => SetField(ref _driverVersion, value); }

    public string Capabilities { get => _capabilities; private set => SetField(ref _capabilities, value); }

    public string ProcessId { get => _processId; set => SetField(ref _processId, value); }

    public string SingleAddress { get => _singleAddress; set => SetField(ref _singleAddress, value); }

    public string SelectedType { get => _selectedType; set => SetField(ref _selectedType, value); }

    public string ReadResult { get => _readResult; private set => SetField(ref _readResult, value); }

    public string WriteValue { get => _writeValue; set => SetField(ref _writeValue, value); }

    public string WriteResult { get => _writeResult; private set => SetField(ref _writeResult, value); }

    public string LastOperation { get => _lastOperation; private set => SetField(ref _lastOperation, value); }

    public void Connect()
    {
        try
        {
            _api.Open();
            GetProtocolVersionResponse protocol = _api.GetProtocolVersion();
            GetCapabilitiesResponse capabilities = _api.GetCapabilities();
            PingResponse ping = _api.Ping();

            ProtocolVersion = FormatVersion(protocol.Header.ProtocolVersion);
            DriverVersion =
                $"{ping.DriverVersion.Major}.{ping.DriverVersion.Minor}." +
                $"{ping.DriverVersion.Build}.{ping.DriverVersion.Revision}";
            Capabilities = $"0x{(ulong)capabilities.Capabilities:X16}";
            ConnectionStatus = "Driver Connected";
            LastOperation = FormatSuccess("Connect", null, "Protocol and PING checks succeeded.");
        }
        catch (Exception exception) when (exception is DriverApiException or InvalidDataException)
        {
            _api.Close();
            ConnectionStatus = "Driver Disconnected";
            ProtocolVersion = "—";
            DriverVersion = "—";
            Capabilities = "—";
            LastOperation = FormatError("Connect", exception, null);
        }
        finally
        {
            OnPropertyChanged(nameof(IsConnected));
        }
    }

    public void Disconnect()
    {
        _api.Close();
        ConnectionStatus = "Driver Disconnected";
        ProtocolVersion = "—";
        DriverVersion = "—";
        Capabilities = "—";
        LastOperation = FormatSuccess("Disconnect", TryParseProcessId(), "Device handle closed.");
        OnPropertyChanged(nameof(IsConnected));
    }

    public void ReadSingle()
    {
        uint? processId = TryParseProcessId();
        try
        {
            uint pid = ParseProcessId();
            ulong address = ParseAddress(SingleAddress);
            ReadResult = SelectedType switch
            {
                "Int32" => _api.ReadInt32(pid, address).ToString(CultureInfo.InvariantCulture),
                "Int64" => _api.ReadInt64(pid, address).ToString(CultureInfo.InvariantCulture),
                "Float32" => _api.ReadFloat32(pid, address).ToString("R", CultureInfo.InvariantCulture),
                _ => throw new FormatException($"Unsupported type: {SelectedType}."),
            };
            LastOperation = FormatSuccess("ReadSingle", pid, $"Value={ReadResult}");
        }
        catch (Exception exception) when (IsDisplayableException(exception))
        {
            ReadResult = "Error";
            LastOperation = FormatError("ReadSingle", exception, processId);
        }
    }

    public void WriteSingle()
    {
        uint? processId = TryParseProcessId();
        try
        {
            uint pid = ParseProcessId();
            ulong address = ParseAddress(SingleAddress);
            WriteSingleResponse response = SelectedType switch
            {
                "Int32" => _api.WriteInt32(pid, address, int.Parse(WriteValue, CultureInfo.InvariantCulture)),
                "Int64" => _api.WriteInt64(pid, address, long.Parse(WriteValue, CultureInfo.InvariantCulture)),
                "Float32" => _api.WriteFloat32(pid, address, float.Parse(WriteValue, CultureInfo.InvariantCulture)),
                _ => throw new FormatException($"Unsupported type: {SelectedType}."),
            };
            WriteResult = $"{response.Header.OperationStatus}, {response.Header.BytesProcessed} bytes";
            LastOperation = FormatSuccess("WriteSingle", pid, WriteResult);
        }
        catch (Exception exception) when (IsDisplayableException(exception))
        {
            WriteResult = "Error";
            LastOperation = FormatError("WriteSingle", exception, processId);
        }
    }

    public void ReadBatch()
    {
        uint? processId = TryParseProcessId();
        try
        {
            uint pid = ParseProcessId();
            BatchReadRequestItem[] requests = BatchRows
                .Select(row => new BatchReadRequestItem(ParseAddress(row.Address), GetTypeSize(row.Type)))
                .ToArray();
            BatchReadResponseMessage response = _api.ReadBatch(pid, requests);

            for (int index = 0; index < BatchRows.Count; index++)
            {
                BatchMemoryRowViewModel row = BatchRows[index];
                BatchItemResult result = response.Results[index];
                row.Status = $"{result.OperationStatus}, {result.BytesProcessed} bytes";
                row.ReadValue = result.OperationStatus == OperationStatus.Success
                    ? DecodeBatchValue(response, result, row.Type)
                    : string.Empty;
            }

            LastOperation = FormatSuccess(
                "ReadBatch",
                pid,
                $"Overall={response.Header.Header.OperationStatus}; Bytes={response.Header.Header.BytesProcessed}");
        }
        catch (Exception exception) when (IsDisplayableException(exception))
        {
            LastOperation = FormatError("ReadBatch", exception, processId);
        }
    }

    public void WriteBatch()
    {
        uint? processId = TryParseProcessId();
        try
        {
            uint pid = ParseProcessId();
            BatchWriteRequestItem[] requests = BatchRows
                .Select(row => new BatchWriteRequestItem(ParseAddress(row.Address), EncodeValue(row.Type, row.WriteValue)))
                .ToArray();
            BatchWriteResponseMessage response = _api.WriteBatch(pid, requests);

            for (int index = 0; index < BatchRows.Count; index++)
            {
                BatchItemResult result = response.Results[index];
                BatchRows[index].Status = $"{result.OperationStatus}, {result.BytesProcessed} bytes";
            }

            LastOperation = FormatSuccess(
                "WriteBatch",
                pid,
                $"Overall={response.Header.Header.OperationStatus}; Bytes={response.Header.Header.BytesProcessed}");
        }
        catch (Exception exception) when (IsDisplayableException(exception))
        {
            LastOperation = FormatError("WriteBatch", exception, processId);
        }
    }

    public void Dispose() => _api.Dispose();

    public static ulong ParseAddress(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new FormatException("Address is required.");
        }

        ReadOnlySpan<char> value = text.AsSpan().Trim();
        ulong address = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? ulong.Parse(value[2..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture)
            : ulong.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
        return address != 0 ? address : throw new FormatException("Address must be nonzero.");
    }

    private static string DecodeBatchValue(BatchReadResponseMessage response, BatchItemResult result, string type)
    {
        int offset = checked((int)(result.DataOffset - response.Header.DataOffset));
        ReadOnlySpan<byte> data = response.Data.Span.Slice(offset, checked((int)result.BytesProcessed));
        return type switch
        {
            "Int32" => BinaryPrimitives.ReadInt32LittleEndian(data).ToString(CultureInfo.InvariantCulture),
            "Int64" => BinaryPrimitives.ReadInt64LittleEndian(data).ToString(CultureInfo.InvariantCulture),
            "Float32" => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(data))
                .ToString("R", CultureInfo.InvariantCulture),
            _ => throw new FormatException($"Unsupported type: {type}."),
        };
    }

    private static byte[] EncodeValue(string type, string value) =>
        type switch
        {
            "Int32" => EncodeInt32(int.Parse(value, CultureInfo.InvariantCulture)),
            "Int64" => EncodeInt64(long.Parse(value, CultureInfo.InvariantCulture)),
            "Float32" => EncodeInt32(BitConverter.SingleToInt32Bits(float.Parse(value, CultureInfo.InvariantCulture))),
            _ => throw new FormatException($"Unsupported type: {type}."),
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

    private static uint GetTypeSize(string type) => type switch
    {
        "Int32" => sizeof(int),
        "Int64" => sizeof(long),
        "Float32" => sizeof(float),
        _ => throw new FormatException($"Unsupported type: {type}."),
    };

    private uint ParseProcessId()
    {
        if (!uint.TryParse(ProcessId, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint processId) ||
            processId == 0)
        {
            throw new FormatException("PID must be a nonzero UInt32 value.");
        }

        return processId;
    }

    private uint? TryParseProcessId() =>
        uint.TryParse(ProcessId, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint processId)
            ? processId
            : null;

    private static bool IsDisplayableException(Exception exception) =>
        exception is DriverApiException or FormatException or OverflowException or InvalidDataException or ArgumentException;

    private static string FormatVersion(ProtocolVersion version) => $"{version.Major}.{version.Minor}";

    private static string FormatSuccess(string operation, uint? processId, string message) =>
        $"[{DateTimeOffset.Now:O}] Operation={operation}; DriverStatus=Success; " +
        $"Win32Error=None; TargetPID={FormatPid(processId)}; {message}";

    private static string FormatError(string operation, Exception exception, uint? fallbackProcessId)
    {
        if (exception is DriverApiException driverException)
        {
            return $"[{driverException.Timestamp:O}] Operation={driverException.Operation}; " +
                $"DriverStatus={driverException.DriverStatus?.ToString() ?? "N/A"}; " +
                $"DetailStatus=0x{driverException.DetailStatus:X8}; " +
                $"Win32Error={driverException.Win32Error?.ToString(CultureInfo.InvariantCulture) ?? "N/A"}; " +
                $"TargetPID={FormatPid(driverException.TargetProcessId ?? fallbackProcessId)}; " +
                driverException.Message;
        }

        return $"[{DateTimeOffset.Now:O}] Operation={operation}; DriverStatus=N/A; " +
            $"Win32Error=N/A; TargetPID={FormatPid(fallbackProcessId)}; {exception.Message}";
    }

    private static string FormatPid(uint? processId) =>
        processId?.ToString(CultureInfo.InvariantCulture) ?? "N/A";

    private void SetField(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        if (string.Equals(field, value, StringComparison.Ordinal))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
