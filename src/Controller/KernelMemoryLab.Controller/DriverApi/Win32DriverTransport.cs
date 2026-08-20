using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using KernelMemoryLab.Protocol;
using Microsoft.Win32.SafeHandles;

namespace KernelMemoryLab.Controller.DriverApi;

internal sealed class Win32DriverTransport : IDriverTransport
{
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint OpenExisting = 3;

    private SafeFileHandle? _device;

    public bool IsOpen => _device is { IsInvalid: false, IsClosed: false };

    public void Open()
    {
        if (IsOpen)
        {
            return;
        }

        Close();
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

        _device = device;
    }

    public void Close()
    {
        _device?.Dispose();
        _device = null;
    }

    public byte[] Invoke(uint ioControlCode, ReadOnlySpan<byte> input, int outputCapacity)
    {
        if (!IsOpen || _device is null)
        {
            throw new InvalidOperationException("The Driver device is not open.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(outputCapacity);

        byte[] inputBuffer = input.ToArray();
        byte[] outputBuffer = new byte[outputCapacity];
        bool succeeded = NativeMethods.DeviceIoControl(
            _device,
            ioControlCode,
            inputBuffer,
            checked((uint)inputBuffer.Length),
            outputBuffer,
            checked((uint)outputBuffer.Length),
            out uint bytesReturned,
            IntPtr.Zero);

        if (!succeeded)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        if (bytesReturned > outputBuffer.Length)
        {
            throw new InvalidDataException("Driver returned more bytes than the output buffer capacity.");
        }

        return outputBuffer.AsSpan(0, checked((int)bytesReturned)).ToArray();
    }

    public void Dispose() => Close();

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
