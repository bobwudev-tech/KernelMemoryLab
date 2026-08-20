using System.Runtime.InteropServices;
using KernelMemoryLab.Target;

namespace KernelMemoryLab.Target.Tests;

internal static class Program
{
    private static readonly (string Name, Action Test)[] Tests =
    {
        (nameof(LayoutIsFixedAndNonOverlapping), LayoutIsFixedAndNonOverlapping),
        (nameof(InitialValuesAreCorrect), InitialValuesAreCorrect),
        (nameof(SelfMemoryWritesRoundTrip), SelfMemoryWritesRoundTrip),
        (nameof(RefreshReadsUnmanagedMemoryInsteadOfCachedValues), RefreshReadsUnmanagedMemoryInsteadOfCachedValues),
        (nameof(AddressesRemainStableForBlockLifetime), AddressesRemainStableForBlockLifetime),
        (nameof(DisposedBlockRejectsAccess), DisposedBlockRejectsAccess),
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

        Console.WriteLine($"Target tests: {Tests.Length - failures} passed, {failures} failed.");
        return failures == 0 ? 0 : 1;
    }

    private static void LayoutIsFixedAndNonOverlapping()
    {
        AssertEqual(24, TargetMemoryLayout.BlockSize);
        AssertDefinition(TargetVariableId.Health, "Health", TargetValueType.Signed32, 0, 4);
        AssertDefinition(TargetVariableId.Mana, "Mana", TargetValueType.Signed32, 4, 4);
        AssertDefinition(TargetVariableId.Gold, "Gold", TargetValueType.Signed64, 8, 8);
        AssertDefinition(TargetVariableId.PositionX, "PositionX", TargetValueType.Real32, 16, 4);
        AssertDefinition(TargetVariableId.PositionY, "PositionY", TargetValueType.Real32, 20, 4);

        TargetVariableDefinition[] ordered = TargetMemoryLayout.Variables
            .OrderBy(variable => variable.Offset)
            .ToArray();

        for (int index = 1; index < ordered.Length; index++)
        {
            int previousEnd = checked(ordered[index - 1].Offset + ordered[index - 1].Size);
            AssertTrue(previousEnd <= ordered[index].Offset, "Target variables overlap.");
        }

        TargetVariableDefinition last = ordered[^1];
        AssertEqual(TargetMemoryLayout.BlockSize, checked(last.Offset + last.Size));
    }

    private static void InitialValuesAreCorrect()
    {
        using TargetMemoryBlock block = new();

        AssertEqual(100, ReadValue<int>(block, TargetVariableId.Health));
        AssertEqual(50, ReadValue<int>(block, TargetVariableId.Mana));
        AssertEqual(1_000L, ReadValue<long>(block, TargetVariableId.Gold));
        AssertEqual(10.0f, ReadValue<float>(block, TargetVariableId.PositionX));
        AssertEqual(20.0f, ReadValue<float>(block, TargetVariableId.PositionY));
    }

    private static void SelfMemoryWritesRoundTrip()
    {
        using TargetMemoryBlock block = new();

        block.WriteInt32(TargetVariableId.Health, 777);
        block.WriteInt32(TargetVariableId.Mana, -25);
        block.WriteInt64(TargetVariableId.Gold, 9_876_543_210L);
        block.WriteFloat32(TargetVariableId.PositionX, -12.5f);
        block.WriteFloat32(TargetVariableId.PositionY, 42.25f);

        AssertEqual(777, ReadValue<int>(block, TargetVariableId.Health));
        AssertEqual(-25, ReadValue<int>(block, TargetVariableId.Mana));
        AssertEqual(9_876_543_210L, ReadValue<long>(block, TargetVariableId.Gold));
        AssertEqual(-12.5f, ReadValue<float>(block, TargetVariableId.PositionX));
        AssertEqual(42.25f, ReadValue<float>(block, TargetVariableId.PositionY));
    }

    private static void RefreshReadsUnmanagedMemoryInsteadOfCachedValues()
    {
        using TargetMemoryBlock block = new();
        MainWindowViewModel viewModel = new(block);
        TargetVariableRowViewModel health = viewModel.Variables.Single(
            variable => variable.Definition.Id == TargetVariableId.Health);

        AssertEqual("100", health.CurrentValue);
        Marshal.WriteInt32(block.GetAddress(TargetVariableId.Health), 777);
        viewModel.Refresh();
        AssertEqual("777", health.CurrentValue);
    }

    private static void AddressesRemainStableForBlockLifetime()
    {
        using TargetMemoryBlock block = new();
        IntPtr baseAddress = block.BaseAddress;
        IntPtr[] addresses = TargetMemoryLayout.Variables
            .Select(variable => block.GetAddress(variable.Id))
            .ToArray();

        for (int iteration = 0; iteration < 10; iteration++)
        {
            _ = block.ReadAll();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            AssertEqual(baseAddress, block.BaseAddress);
            for (int index = 0; index < addresses.Length; index++)
            {
                AssertEqual(addresses[index], block.GetAddress(TargetMemoryLayout.Variables[index].Id));
            }
        }
    }

    private static void DisposedBlockRejectsAccess()
    {
        TargetMemoryBlock block = new();
        block.Dispose();
        AssertThrows<ObjectDisposedException>(() => _ = block.BaseAddress);
        AssertThrows<ObjectDisposedException>(() => block.ReadAll());
    }

    private static T ReadValue<T>(TargetMemoryBlock block, TargetVariableId id) =>
        (T)block.Read(id).Value;

    private static void AssertDefinition(
        TargetVariableId id,
        string name,
        TargetValueType type,
        int offset,
        int size)
    {
        TargetVariableDefinition definition = TargetMemoryLayout.GetDefinition(id);
        AssertEqual(name, definition.Name);
        AssertEqual(type, definition.Type);
        AssertEqual(offset, definition.Offset);
        AssertEqual(size, definition.Size);
    }

    private static void AssertEqual<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected {expected}, actual {actual}.");
        }
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
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
