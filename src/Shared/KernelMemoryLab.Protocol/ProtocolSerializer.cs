using System.Runtime.InteropServices;

namespace KernelMemoryLab.Protocol;

/// <summary>
/// Serializes fixed-width, pointer-free protocol structures using the Windows
/// little-endian in-memory representation.
/// </summary>
public static class ProtocolSerializer
{
    public static byte[] Serialize<T>(in T value)
        where T : unmanaged
    {
        int structureSize = Marshal.SizeOf<T>();
        byte[] buffer = new byte[structureSize];
        MemoryMarshal.Write(buffer.AsSpan(), in value);
        return buffer;
    }

    public static T Deserialize<T>(ReadOnlySpan<byte> buffer)
        where T : unmanaged
    {
        int structureSize = Marshal.SizeOf<T>();
        if (buffer.Length != structureSize)
        {
            throw new ArgumentException(
                $"Expected exactly {structureSize} bytes for {typeof(T).Name}.",
                nameof(buffer));
        }

        return MemoryMarshal.Read<T>(buffer);
    }
}

