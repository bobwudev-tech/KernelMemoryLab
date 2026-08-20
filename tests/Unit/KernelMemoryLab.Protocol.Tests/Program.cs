using System.Buffers.Binary;
using System.Runtime.InteropServices;
using KernelMemoryLab.Protocol;

namespace KernelMemoryLab.Protocol.Tests;

internal static class Program
{
    private static readonly (string Name, Action Test)[] Tests =
    {
        (nameof(StructureSizesAreStable), StructureSizesAreStable),
        (nameof(IoControlCodesAreStable), IoControlCodesAreStable),
        (nameof(PingSerializationIsStable), PingSerializationIsStable),
        (nameof(SerializationRoundTrips), SerializationRoundTrips),
        (nameof(DeserializeRejectsWrongSize), DeserializeRejectsWrongSize),
        (nameof(LimitsAndCapabilitiesAreSafe), LimitsAndCapabilitiesAreSafe),
    };

    private static int Main()
    {
        int failures = 0;

        foreach ((string name, Action test) in Tests)
        {
            try
            {
                test();
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception exception)
            {
                failures++;
                Console.Error.WriteLine($"FAIL {name}: {exception.Message}");
            }
        }

        Console.WriteLine($"Protocol tests: {Tests.Length - failures} passed, {failures} failed.");
        return failures == 0 ? 0 : 1;
    }

    private static void StructureSizesAreStable()
    {
        AssertEqual(4, Marshal.SizeOf<ProtocolVersion>());
        AssertEqual(8, Marshal.SizeOf<DriverVersion>());
        AssertEqual(16, Marshal.SizeOf<CommonRequestHeader>());
        AssertEqual(16, Marshal.SizeOf<CommonResponseHeader>());
        AssertEqual(16, Marshal.SizeOf<GetProtocolVersionRequest>());
        AssertEqual(16, Marshal.SizeOf<GetProtocolVersionResponse>());
        AssertEqual(16, Marshal.SizeOf<GetCapabilitiesRequest>());
        AssertEqual(40, Marshal.SizeOf<GetCapabilitiesResponse>());
        AssertEqual(24, Marshal.SizeOf<PingRequest>());
        AssertEqual(40, Marshal.SizeOf<PingResponse>());
    }

    private static void IoControlCodesAreStable()
    {
        AssertEqual(0x0022E000u, IoControlCodes.GetProtocolVersion);
        AssertEqual(0x0022E004u, IoControlCodes.GetCapabilities);
        AssertEqual(0x0022E008u, IoControlCodes.Ping);
        AssertEqual(0x0022E040u, IoControlCodes.ReadSingle);
        AssertEqual(0x0022E044u, IoControlCodes.WriteSingle);
        AssertEqual(0x0022E048u, IoControlCodes.ReadBatch);
        AssertEqual(0x0022E04Cu, IoControlCodes.WriteBatch);
    }

    private static void PingSerializationIsStable()
    {
        const ulong token = 0x0123456789ABCDEFUL;
        PingRequest request = PingRequest.Create(token);
        byte[] buffer = ProtocolSerializer.Serialize(in request);

        AssertEqual(24, buffer.Length);
        AssertEqual((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(0, 2)));
        AssertEqual((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(2, 2)));
        AssertEqual(24u, BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(4, 4)));
        AssertEqual(0u, BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(8, 4)));
        AssertEqual(0u, BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(12, 4)));
        AssertEqual(token, BinaryPrimitives.ReadUInt64LittleEndian(buffer.AsSpan(16, 8)));
    }

    private static void SerializationRoundTrips()
    {
        const ulong token = 0xA5A55A5AF00DBAADUL;
        PingRequest request = PingRequest.Create(token);
        PingRequest roundTrip = ProtocolSerializer.Deserialize<PingRequest>(
            ProtocolSerializer.Serialize(in request));

        AssertEqual(ProtocolConstants.ProtocolMajor, roundTrip.Header.ProtocolVersion.Major);
        AssertEqual(ProtocolConstants.ProtocolMinor, roundTrip.Header.ProtocolVersion.Minor);
        AssertEqual(24u, roundTrip.Header.StructureSize);
        AssertEqual(0u, roundTrip.Header.Flags);
        AssertEqual(0u, roundTrip.Header.Reserved);
        AssertEqual(token, roundTrip.Token);
    }

    private static void DeserializeRejectsWrongSize()
    {
        AssertThrows<ArgumentException>(() =>
            ProtocolSerializer.Deserialize<PingRequest>(new byte[23]));
        AssertThrows<ArgumentException>(() =>
            ProtocolSerializer.Deserialize<PingRequest>(new byte[25]));
    }

    private static void LimitsAndCapabilitiesAreSafe()
    {
        AssertEqual(4_096u, ProtocolConstants.MaxSingleItemSize);
        AssertEqual(128u, ProtocolConstants.MaxBatchItems);
        AssertEqual(
            524_288u,
            checked(ProtocolConstants.MaxSingleItemSize * ProtocolConstants.MaxBatchItems));
        AssertEqual(524_288u, ProtocolConstants.MaxBatchPayloadSize);

        ProtocolCapabilities expected =
            ProtocolCapabilities.GetProtocolVersion |
            ProtocolCapabilities.GetCapabilities |
            ProtocolCapabilities.Ping;

        AssertEqual(expected, ProtocolConstants.Phase02Capabilities);
        AssertEqual(
            ProtocolCapabilities.None,
            ProtocolConstants.Phase02Capabilities &
            (ProtocolCapabilities.ReadSingle |
             ProtocolCapabilities.WriteSingle |
             ProtocolCapabilities.ReadBatch |
             ProtocolCapabilities.WriteBatch));
    }

    private static void AssertEqual<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
        {
            throw new InvalidOperationException($"Expected {expected}, actual {actual}.");
        }
    }

    private static void AssertThrows<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"Expected exception {typeof(TException).Name}.");
    }
}
