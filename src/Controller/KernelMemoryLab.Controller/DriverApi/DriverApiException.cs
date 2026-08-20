using KernelMemoryLab.Protocol;

namespace KernelMemoryLab.Controller.DriverApi;

public sealed class DriverApiException : Exception
{
    public DriverApiException(
        string operation,
        string message,
        OperationStatus? driverStatus = null,
        int? win32Error = null,
        uint? targetProcessId = null,
        uint detailStatus = 0,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Operation = operation;
        DriverStatus = driverStatus;
        Win32Error = win32Error;
        TargetProcessId = targetProcessId;
        DetailStatus = detailStatus;
        Timestamp = DateTimeOffset.Now;
    }

    public string Operation { get; }

    public OperationStatus? DriverStatus { get; }

    public int? Win32Error { get; }

    public uint? TargetProcessId { get; }

    public uint DetailStatus { get; }

    public DateTimeOffset Timestamp { get; }
}
