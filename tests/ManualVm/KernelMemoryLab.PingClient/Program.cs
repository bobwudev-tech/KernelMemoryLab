using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using KernelMemoryLab.Protocol;
using Microsoft.Win32.SafeHandles;

namespace KernelMemoryLab.PingClient;

/// <summary>
/// Manual VM-only transport harness. The Coding Agent may compile this project
/// but must never execute it against a real KernelMemoryLab device.
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
            string command = args.Length == 0 ? "ping" : args[0].ToLowerInvariant();
            using SafeFileHandle device = OpenDevice();

            return command switch
            {
                "version" => RunVersion(device),
                "capabilities" => RunCapabilities(device),
                "ping" => RunPing(device, ParseToken(args), OperationStatus.Success),
                "version-mismatch" => RunVersionMismatch(device),
                "malformed-size" => RunMalformedSize(device),
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

    private static int RunVersion(SafeFileHandle device)
    {
        GetProtocolVersionRequest request = GetProtocolVersionRequest.Create();
        GetProtocolVersionResponse response = Invoke<
            GetProtocolVersionRequest,
            GetProtocolVersionResponse>(device, IoControlCodes.GetProtocolVersion, request);

        PrintHeader(response.Header);
        return response.Header.OperationStatus == OperationStatus.Success ? 0 : 1;
    }

    private static int RunCapabilities(SafeFileHandle device)
    {
        GetCapabilitiesRequest request = GetCapabilitiesRequest.Create();
        GetCapabilitiesResponse response = Invoke<
            GetCapabilitiesRequest,
            GetCapabilitiesResponse>(device, IoControlCodes.GetCapabilities, request);

        PrintHeader(response.Header);
        Console.WriteLine($"Capabilities: 0x{(ulong)response.Capabilities:X16}");
        Console.WriteLine($"MaxSingleItemSize: {response.MaxSingleItemSize}");
        Console.WriteLine($"MaxBatchItems: {response.MaxBatchItems}");
        Console.WriteLine($"MaxBatchPayloadSize: {response.MaxBatchPayloadSize}");
        return response.Header.OperationStatus == OperationStatus.Success ? 0 : 1;
    }

    private static int RunPing(
        SafeFileHandle device,
        ulong token,
        OperationStatus expectedStatus
    )
    {
        PingRequest request = PingRequest.Create(token);
        return RunPingRequest(device, request, expectedStatus);
    }

    private static int RunVersionMismatch(SafeFileHandle device)
    {
        PingRequest request = PingRequest.Create(0x1122334455667788UL);
        request.Header.ProtocolVersion.Major++;
        return RunPingRequest(
            device,
            request,
            OperationStatus.ProtocolMismatch);
    }

    private static int RunMalformedSize(SafeFileHandle device)
    {
        PingRequest request = PingRequest.Create(0x8877665544332211UL);
        request.Header.StructureSize--;
        return RunPingRequest(
            device,
            request,
            OperationStatus.InvalidStructureSize);
    }

    private static int RunPingRequest(
        SafeFileHandle device,
        PingRequest request,
        OperationStatus expectedStatus
    )
    {
        PingResponse response = Invoke<PingRequest, PingResponse>(
            device,
            IoControlCodes.Ping,
            request);

        PrintHeader(response.Header);
        Console.WriteLine(
            $"DriverVersion: {response.DriverVersion.Major}." +
            $"{response.DriverVersion.Minor}." +
            $"{response.DriverVersion.Build}." +
            $"{response.DriverVersion.Revision}");
        Console.WriteLine($"Capabilities: 0x{(ulong)response.Capabilities:X16}");
        Console.WriteLine($"EchoToken: 0x{response.EchoToken:X16}");

        bool statusMatches = response.Header.OperationStatus == expectedStatus;
        bool tokenMatches = expectedStatus != OperationStatus.Success ||
            response.EchoToken == request.Token;

        return statusMatches && tokenMatches ? 0 : 1;
    }

    private static TResponse Invoke<TRequest, TResponse>(
        SafeFileHandle device,
        uint ioControlCode,
        TRequest request
    )
        where TRequest : unmanaged
        where TResponse : unmanaged
    {
        byte[] input = ProtocolSerializer.Serialize(in request);
        byte[] output = new byte[Marshal.SizeOf<TResponse>()];

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

        if (bytesReturned != output.Length)
        {
            throw new InvalidDataException(
                $"Expected {output.Length} response bytes, received {bytesReturned}.");
        }

        return ProtocolSerializer.Deserialize<TResponse>(output);
    }

    private static ulong ParseToken(string[] args)
    {
        if (args.Length < 2)
        {
            return 0x0123456789ABCDEFUL;
        }

        string tokenText = args[1];
        if (tokenText.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return ulong.Parse(
                tokenText.AsSpan(2),
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture);
        }

        return ulong.Parse(tokenText, CultureInfo.InvariantCulture);
    }

    private static void PrintHeader(CommonResponseHeader header)
    {
        Console.WriteLine(
            $"ProtocolVersion: {header.ProtocolVersion.Major}." +
            $"{header.ProtocolVersion.Minor}");
        Console.WriteLine($"OperationStatus: {header.OperationStatus}");
        Console.WriteLine($"BytesProcessed: {header.BytesProcessed}");
        Console.WriteLine($"DetailStatus: 0x{header.DetailStatus:X8}");
    }

    private static int PrintUsage()
    {
        Console.Error.WriteLine(
            "Usage: KernelMemoryLab.PingClient " +
            "[version|capabilities|ping [token]|version-mismatch|malformed-size]");
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

