using System.Buffers.Binary;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using KernelMemoryLab.Protocol;
using Microsoft.Win32.SafeHandles;

namespace KernelMemoryLab.SingleMemoryClient;

/// <summary>
/// Manual VM-only transport harness. The Coding Agent may compile this project
/// but must never execute it because it opens the real device and calls IOCTLs.
/// </summary>
internal static class Program
{
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint OpenExisting = 3;

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length < 4)
            {
                return PrintUsage();
            }

            string command = args[0].ToLowerInvariant();
            uint processId = uint.Parse(args[1], CultureInfo.InvariantCulture);
            ulong address = ParseUnsigned(args[2]);

            using SafeFileHandle device = OpenDevice();
            return command switch
            {
                "read-int32" => ReadTyped(device, processId, address, sizeof(int), "Int32"),
                "read-int64" => ReadTyped(device, processId, address, sizeof(long), "Int64"),
                "read-float32" => ReadTyped(device, processId, address, sizeof(float), "Float32"),
                "read-raw" => ReadRaw(device, processId, address, ParseSize(args[3])),
                "write-int32" => WriteInt32(device, processId, address, args[3]),
                "write-int64" => WriteInt64(device, processId, address, args[3]),
                "write-float32" => WriteFloat32(device, processId, address, args[3]),
                _ => PrintUsage(),
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static SafeFileHandle OpenDevice()
    {
        SafeFileHandle device = NativeMethods.CreateFile(
            ProtocolConstants.DevicePath,
            GenericRead | GenericWrite,
            0,
            IntPtr.Zero,
            OpenExisting,
            0,
            IntPtr.Zero);

        if (device.IsInvalid)
        {
            int error = Marshal.GetLastWin32Error();
            device.Dispose();
            throw new Win32Exception(error, $"Unable to open {ProtocolConstants.DevicePath}.");
        }

        return device;
    }

    private static int ReadTyped(
        SafeFileHandle device,
        uint processId,
        ulong address,
        uint size,
        string valueType)
    {
        ReadSingleMessage response = InvokeRead(device, processId, address, size);
        PrintHeader(response.Header);

        if (response.Header.OperationStatus != OperationStatus.Success)
        {
            return 1;
        }

        ReadOnlySpan<byte> data = response.Data.Span;
        string value = valueType switch
        {
            "Int32" => BinaryPrimitives.ReadInt32LittleEndian(data).ToString(CultureInfo.InvariantCulture),
            "Int64" => BinaryPrimitives.ReadInt64LittleEndian(data).ToString(CultureInfo.InvariantCulture),
            "Float32" => BitConverter.Int32BitsToSingle(
                    BinaryPrimitives.ReadInt32LittleEndian(data))
                .ToString("R", CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException($"Unsupported value type: {valueType}."),
        };

        Console.WriteLine($"Value ({valueType}): {value}");
        return 0;
    }

    private static int ReadRaw(
        SafeFileHandle device,
        uint processId,
        ulong address,
        uint size)
    {
        ReadSingleMessage response = InvokeRead(device, processId, address, size);
        PrintHeader(response.Header);

        if (!response.Data.IsEmpty)
        {
            Console.WriteLine($"Data: {Convert.ToHexString(response.Data.Span)}");
        }

        return response.Header.OperationStatus == OperationStatus.Success ? 0 : 1;
    }

    private static ReadSingleMessage InvokeRead(
        SafeFileHandle device,
        uint processId,
        ulong address,
        uint size)
    {
        if (size > ProtocolConstants.MaxSingleItemSize + 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(size),
                "Manual harness only permits the protocol maximum plus one for the negative test.");
        }

        ReadSingleRequest request = ReadSingleRequest.Create(processId, address, size);
        byte[] input = ProtocolSerializer.Serialize(in request);
        byte[] output = new byte[checked(SingleMemoryProtocol.ResponseHeaderSize + (int)size)];
        uint bytesReturned = Invoke(device, IoControlCodes.ReadSingle, input, output);
        return SingleMemoryProtocol.DecodeReadResponse(
            output.AsSpan(0, checked((int)bytesReturned)));
    }

    private static int WriteInt32(
        SafeFileHandle device,
        uint processId,
        ulong address,
        string valueText)
    {
        byte[] data = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(
            data,
            int.Parse(valueText, CultureInfo.InvariantCulture));
        return InvokeWrite(device, processId, address, data);
    }

    private static int WriteInt64(
        SafeFileHandle device,
        uint processId,
        ulong address,
        string valueText)
    {
        byte[] data = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(
            data,
            long.Parse(valueText, CultureInfo.InvariantCulture));
        return InvokeWrite(device, processId, address, data);
    }

    private static int WriteFloat32(
        SafeFileHandle device,
        uint processId,
        ulong address,
        string valueText)
    {
        byte[] data = new byte[sizeof(float)];
        BinaryPrimitives.WriteInt32LittleEndian(
            data,
            BitConverter.SingleToInt32Bits(
                float.Parse(valueText, CultureInfo.InvariantCulture)));
        return InvokeWrite(device, processId, address, data);
    }

    private static int InvokeWrite(
        SafeFileHandle device,
        uint processId,
        ulong address,
        ReadOnlySpan<byte> data)
    {
        byte[] input = SingleMemoryProtocol.EncodeWriteRequest(processId, address, data);
        byte[] output = new byte[Marshal.SizeOf<WriteSingleResponse>()];
        uint bytesReturned = Invoke(device, IoControlCodes.WriteSingle, input, output);

        if (bytesReturned != output.Length)
        {
            throw new InvalidDataException(
                $"Expected {output.Length} response bytes, received {bytesReturned}.");
        }

        WriteSingleResponse response = ProtocolSerializer.Deserialize<WriteSingleResponse>(output);
        PrintHeader(response.Header);
        return response.Header.OperationStatus == OperationStatus.Success ? 0 : 1;
    }

    private static uint Invoke(
        SafeFileHandle device,
        uint ioControlCode,
        byte[] input,
        byte[] output)
    {
        bool succeeded = NativeMethods.DeviceIoControl(
            device,
            ioControlCode,
            input,
            checked((uint)input.Length),
            output,
            checked((uint)output.Length),
            out uint bytesReturned,
            IntPtr.Zero);

        if (!succeeded)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return bytesReturned;
    }

    private static uint ParseSize(string text) =>
        uint.Parse(text, CultureInfo.InvariantCulture);

    private static ulong ParseUnsigned(string text)
    {
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return ulong.Parse(
                text.AsSpan(2),
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture);
        }

        return ulong.Parse(text, CultureInfo.InvariantCulture);
    }

    private static void PrintHeader(CommonResponseHeader header)
    {
        Console.WriteLine(
            $"ProtocolVersion: {header.ProtocolVersion.Major}.{header.ProtocolVersion.Minor}");
        Console.WriteLine($"OperationStatus: {header.OperationStatus}");
        Console.WriteLine($"BytesProcessed: {header.BytesProcessed}");
        Console.WriteLine($"DetailStatus: 0x{header.DetailStatus:X8}");
    }

    private static int PrintUsage()
    {
        Console.Error.WriteLine(
            "Usage: KernelMemoryLab.SingleMemoryClient " +
            "<read-int32|read-int64|read-float32|read-raw|" +
            "write-int32|write-int64|write-float32> <pid> <address> <size-or-value>");
        return 2;
    }

    private static class NativeMethods
    {
        [DllImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true)]
        internal static extern SafeFileHandle CreateFile(
            [MarshalAs(UnmanagedType.LPWStr)] string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeviceIoControl(
            SafeFileHandle device,
            uint ioControlCode,
            [In] byte[] inputBuffer,
            uint inputBufferSize,
            [Out] byte[] outputBuffer,
            uint outputBufferSize,
            out uint bytesReturned,
            IntPtr overlapped);
    }
}
