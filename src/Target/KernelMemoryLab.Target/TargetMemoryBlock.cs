using System.Runtime.InteropServices;

namespace KernelMemoryLab.Target;

public sealed class TargetMemoryBlock : IDisposable
{
    private IntPtr _baseAddress;

    public TargetMemoryBlock()
    {
        _baseAddress = Marshal.AllocHGlobal(TargetMemoryLayout.BlockSize);

        try
        {
            Marshal.Copy(new byte[TargetMemoryLayout.BlockSize], 0, _baseAddress, TargetMemoryLayout.BlockSize);
            WriteInt32(TargetVariableId.Health, 100);
            WriteInt32(TargetVariableId.Mana, 50);
            WriteInt64(TargetVariableId.Gold, 1_000);
            WriteFloat32(TargetVariableId.PositionX, 10.0f);
            WriteFloat32(TargetVariableId.PositionY, 20.0f);
        }
        catch
        {
            ReleaseMemory();
            throw;
        }
    }

    ~TargetMemoryBlock() => ReleaseMemory();

    public IntPtr BaseAddress
    {
        get
        {
            ThrowIfDisposed();
            return _baseAddress;
        }
    }

    public IntPtr GetAddress(TargetVariableId id)
    {
        ThrowIfDisposed();
        TargetVariableDefinition definition = TargetMemoryLayout.GetDefinition(id);
        return IntPtr.Add(_baseAddress, definition.Offset);
    }

    public TargetVariableSnapshot Read(TargetVariableId id)
    {
        TargetVariableDefinition definition = TargetMemoryLayout.GetDefinition(id);
        IntPtr address = GetAddress(id);
        object value = definition.Type switch
        {
            TargetValueType.Signed32 => (object)Marshal.ReadInt32(address),
            TargetValueType.Signed64 => (object)Marshal.ReadInt64(address),
            TargetValueType.Real32 => (object)BitConverter.Int32BitsToSingle(Marshal.ReadInt32(address)),
            _ => throw new InvalidOperationException($"Unsupported target value type: {definition.Type}."),
        };

        return new TargetVariableSnapshot(definition, address, value);
    }

    public IReadOnlyList<TargetVariableSnapshot> ReadAll()
    {
        ThrowIfDisposed();
        return TargetMemoryLayout.Variables.Select(variable => Read(variable.Id)).ToArray();
    }

    public void WriteInt32(TargetVariableId id, int value)
    {
        RequireType(id, TargetValueType.Signed32);
        Marshal.WriteInt32(GetAddress(id), value);
    }

    public void WriteInt64(TargetVariableId id, long value)
    {
        RequireType(id, TargetValueType.Signed64);
        Marshal.WriteInt64(GetAddress(id), value);
    }

    public void WriteFloat32(TargetVariableId id, float value)
    {
        RequireType(id, TargetValueType.Real32);
        Marshal.WriteInt32(GetAddress(id), BitConverter.SingleToInt32Bits(value));
    }

    public void Dispose()
    {
        ReleaseMemory();
        GC.SuppressFinalize(this);
    }

    private static void RequireType(TargetVariableId id, TargetValueType requiredType)
    {
        TargetVariableDefinition definition = TargetMemoryLayout.GetDefinition(id);
        if (definition.Type != requiredType)
        {
            throw new ArgumentException(
                $"{definition.Name} is {definition.Type}, not {requiredType}.",
                nameof(id));
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_baseAddress == IntPtr.Zero, this);
    }

    private void ReleaseMemory()
    {
        IntPtr address = Interlocked.Exchange(ref _baseAddress, IntPtr.Zero);
        if (address != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(address);
        }
    }
}

public sealed record TargetVariableSnapshot(
    TargetVariableDefinition Definition,
    IntPtr Address,
    object Value);
