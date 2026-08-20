using System.Buffers.Binary;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using KernelMemoryLab.Protocol;
using Microsoft.Win32.SafeHandles;

namespace KernelMemoryLab.BatchMemoryClient;

/// <summary>
/// Manual VM-only batch transport harness. The Coding Agent may compile this
/// project but must never execute it because it opens the real device and calls IOCTLs.
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
            if (args.Length == 0)
            {
                return PrintUsage();
            }

            string command = args[0].ToLowerInvariant();
            using SafeFileHandle device = OpenDevice();
            return command switch
            {
                "read-five" => RunReadFive(device, args),
                "write-three" => RunWriteThree(device, args),
                "read-invalid-middle" => RunReadInvalidMiddle(device, args),
                "too-many" => RunTooMany(device, args),
                "malformed-offset" => RunMalformedOffset(device, args),
                _ => PrintUsage(),
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static int RunReadFive(SafeFileHandle device, string[] args)
    {
        RequireArgumentCount(args, 7);
        uint processId = ParseProcessId(args[1]);
        BatchReadRequestItem[] items =
        [
            new(ParseUnsigned(args[2]), sizeof(int)),
            new(ParseUnsigned(args[3]), sizeof(int)),
            new(ParseUnsigned(args[4]), sizeof(long)),
            new(ParseUnsigned(args[5]), sizeof(float)),
            new(ParseUnsigned(args[6]), sizeof(float)),
        ];

        BatchReadResponseMessage response = InvokeRead(device, processId, items);
        string[] names = ["Health", "Mana", "Gold", "PositionX", "PositionY"];
        string[] types = ["Int32", "Int32", "Int64", "Float32", "Float32"];
        return PrintReadResponse(response, names, types);
    }

    private static int RunWriteThree(SafeFileHandle device, string[] args)
    {
        RequireArgumentCount(args, 8);
        uint processId = ParseProcessId(args[1]);
        BatchWriteRequestItem[] items =
        [
            new(ParseUnsigned(args[2]), EncodeInt32(args[5])),
            new(ParseUnsigned(args[3]), EncodeInt32(args[6])),
            new(ParseUnsigned(args[4]), EncodeInt64(args[7])),
        ];

        byte[] request = BatchMemoryProtocol.EncodeWriteRequest(processId, items);
        byte[] output = new byte[
            BatchMemoryProtocol.WriteResponseHeaderSize +
            (BatchMemoryProtocol.ItemResultSize * items.Length)];
        uint bytesReturned = Invoke(device, IoControlCodes.WriteBatch, request, output);
        BatchWriteResponseMessage response = BatchMemoryProtocol.DecodeWriteResponse(
            output.AsSpan(0, checked((int)bytesReturned)));

        PrintHeader(response.Header.Header);
        string[] names = ["Health", "Mana", "Gold"];
        for (int index = 0; index < response.Results.Count; index++)
        {
            PrintItemResult(index, names[index], response.Results[index]);
        }

        return response.Header.Header.OperationStatus == OperationStatus.Success ? 0 : 1;
    }

    private static int RunReadInvalidMiddle(SafeFileHandle device, string[] args)
    {
        RequireArgumentCount(args, 4);
        uint processId = ParseProcessId(args[1]);
        BatchReadRequestItem[] items =
        [
            new(ParseUnsigned(args[2]), sizeof(int)),
            new(0, sizeof(int)),
            new(ParseUnsigned(args[3]), sizeof(int)),
        ];

        BatchReadResponseMessage response = InvokeRead(device, processId, items);
        int result = PrintReadResponse(
            response,
            ["Health", "InvalidAddress", "Mana"],
            ["Int32", "Int32", "Int32"]);

        bool expected = response.Header.Header.OperationStatus == OperationStatus.PartialTransfer &&
            response.Results[0].OperationStatus == OperationStatus.Success &&
            response.Results[1].OperationStatus == OperationStatus.InvalidAddress &&
            response.Results[2].OperationStatus == OperationStatus.Success;
        return expected ? 0 : result;
    }

    private static int RunTooMany(SafeFileHandle device, string[] args)
    {
        RequireArgumentCount(args, 3);
        ReadBatchRequestHeader header = new()
        {
            Header = new CommonRequestHeader
            {
                ProtocolVersion = ProtocolConstants.CurrentProtocolVersion,
                StructureSize = BatchMemoryProtocol.ReadRequestHeaderSize,
                Flags = 0,
                Reserved = 0,
            },
            TargetProcessId = ParseProcessId(args[1]),
            ItemCount = ProtocolConstants.MaxBatchItems + 1,
            ItemsOffset = BatchMemoryProtocol.ReadRequestHeaderSize,
            Reserved = 0,
        };

        byte[] request = ProtocolSerializer.Serialize(in header);
        return InvokeExpectedFailure(
            device,
            IoControlCodes.ReadBatch,
            request,
            OperationStatus.InvalidItemCount);
    }

    private static int RunMalformedOffset(SafeFileHandle device, string[] args)
    {
        RequireArgumentCount(args, 3);
        byte[] request = BatchMemoryProtocol.EncodeReadRequest(
            ParseProcessId(args[1]),
            [new BatchReadRequestItem(ParseUnsigned(args[2]), sizeof(int))]);
        BinaryPrimitives.WriteUInt32LittleEndian(
            request.AsSpan(24, 4),
            BatchMemoryProtocol.ReadRequestHeaderSize + 4u);

        return InvokeExpectedFailure(
            device,
            IoControlCodes.ReadBatch,
            request,
            OperationStatus.InvalidOffset);
    }

    private static BatchReadResponseMessage InvokeRead(
        SafeFileHandle device,
        uint processId,
        BatchReadRequestItem[] items)
    {
        byte[] request = BatchMemoryProtocol.EncodeReadRequest(processId, items);
        int dataSize = items.Sum(item => checked((int)item.Size));
        byte[] output = new byte[
            BatchMemoryProtocol.ReadResponseHeaderSize +
            (BatchMemoryProtocol.ItemResultSize * items.Length) +
            dataSize];
        uint bytesReturned = Invoke(device, IoControlCodes.ReadBatch, request, output);
        return BatchMemoryProtocol.DecodeReadResponse(
            output.AsSpan(0, checked((int)bytesReturned)));
    }

    private static int InvokeExpectedFailure(
        SafeFileHandle device,
        uint ioControlCode,
        byte[] request,
        OperationStatus expectedStatus)
    {
        byte[] output = new byte[BatchMemoryProtocol.ReadResponseHeaderSize];
        uint bytesReturned = Invoke(device, ioControlCode, request, output);
        if (bytesReturned != Marshal.SizeOf<CommonResponseHeader>())
        {
            throw new InvalidDataException(
                $"Expected a header-only failure response, received {bytesReturned} bytes.");
        }

        CommonResponseHeader response = ProtocolSerializer.Deserialize<CommonResponseHeader>(
            output.AsSpan(0, checked((int)bytesReturned)));
        PrintHeader(response);
        return response.OperationStatus == expectedStatus ? 0 : 1;
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

    private static int PrintReadResponse(
        BatchReadResponseMessage response,
        string[] names,
        string[] types)
    {
        PrintHeader(response.Header.Header);
        for (int index = 0; index < response.Results.Count; index++)
        {
            BatchItemResult result = response.Results[index];
            PrintItemResult(index, names[index], result);

            if (result.OperationStatus == OperationStatus.Success)
            {
                int relativeOffset = checked(
                    (int)(result.DataOffset - response.Header.DataOffset));
                ReadOnlySpan<byte> data = response.Data.Span.Slice(
                    relativeOffset,
                    checked((int)result.BytesProcessed));
                Console.WriteLine($"  Value ({types[index]}): {FormatValue(types[index], data)}");
            }
        }

        return response.Header.Header.OperationStatus is OperationStatus.Success or OperationStatus.PartialTransfer
            ? 0
            : 1;
    }

    private static void PrintItemResult(int index, string name, BatchItemResult result)
    {
        Console.WriteLine(
            $"Item[{index}] {name}: Status={result.OperationStatus}, " +
            $"Bytes={result.BytesProcessed}, Detail=0x{result.DetailStatus:X8}");
    }

    private static string FormatValue(string type, ReadOnlySpan<byte> data) =>
        type switch
        {
            "Int32" => BinaryPrimitives.ReadInt32LittleEndian(data)
                .ToString(CultureInfo.InvariantCulture),
            "Int64" => BinaryPrimitives.ReadInt64LittleEndian(data)
                .ToString(CultureInfo.InvariantCulture),
            "Float32" => BitConverter.Int32BitsToSingle(
                    BinaryPrimitives.ReadInt32LittleEndian(data))
                .ToString("R", CultureInfo.InvariantCulture),
            _ => Convert.ToHexString(data),
        };

    private static byte[] EncodeInt32(string text)
    {
        byte[] data = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(
            data,
            int.Parse(text, CultureInfo.InvariantCulture));
        return data;
    }

    private static byte[] EncodeInt64(string text)
    {
        byte[] data = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(
            data,
            long.Parse(text, CultureInfo.InvariantCulture));
        return data;
    }

    private static uint ParseProcessId(string text) =>
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

    private static void RequireArgumentCount(string[] args, int expected)
    {
        if (args.Length != expected)
        {
            throw new ArgumentException(
                $"Command requires {expected - 1} arguments; received {args.Length - 1}.");
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
        Console.Error.WriteLine("Commands:");
        Console.Error.WriteLine("  read-five <pid> <health> <mana> <gold> <x> <y>");
        Console.Error.WriteLine(
            "  write-three <pid> <health> <mana> <gold> <healthValue> <manaValue> <goldValue>");
        Console.Error.WriteLine("  read-invalid-middle <pid> <health> <mana>");
        Console.Error.WriteLine("  too-many <pid> <address>");
        Console.Error.WriteLine("  malformed-offset <pid> <address>");
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
