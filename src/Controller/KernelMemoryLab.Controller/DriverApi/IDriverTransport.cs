namespace KernelMemoryLab.Controller.DriverApi;

public interface IDriverTransport : IDisposable
{
    bool IsOpen { get; }

    void Open();

    void Close();

    byte[] Invoke(uint ioControlCode, ReadOnlySpan<byte> input, int outputCapacity);
}
