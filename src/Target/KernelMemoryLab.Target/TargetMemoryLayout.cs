using System.Collections.ObjectModel;

namespace KernelMemoryLab.Target;

public enum TargetVariableId
{
    Health,
    Mana,
    Gold,
    PositionX,
    PositionY,
}

public enum TargetValueType
{
    Signed32,
    Signed64,
    Real32,
}

public sealed record TargetVariableDefinition(
    TargetVariableId Id,
    string Name,
    TargetValueType Type,
    int Offset,
    int Size);

public static class TargetMemoryLayout
{
    public const int HealthOffset = 0;
    public const int ManaOffset = 4;
    public const int GoldOffset = 8;
    public const int PositionXOffset = 16;
    public const int PositionYOffset = 20;
    public const int BlockSize = 24;

    private static readonly ReadOnlyCollection<TargetVariableDefinition> VariableDefinitions =
        Array.AsReadOnly(
        new TargetVariableDefinition[]
        {
            new(TargetVariableId.Health, "Health", TargetValueType.Signed32, HealthOffset, sizeof(int)),
            new(TargetVariableId.Mana, "Mana", TargetValueType.Signed32, ManaOffset, sizeof(int)),
            new(TargetVariableId.Gold, "Gold", TargetValueType.Signed64, GoldOffset, sizeof(long)),
            new(TargetVariableId.PositionX, "PositionX", TargetValueType.Real32, PositionXOffset, sizeof(float)),
            new(TargetVariableId.PositionY, "PositionY", TargetValueType.Real32, PositionYOffset, sizeof(float)),
        });

    public static IReadOnlyList<TargetVariableDefinition> Variables => VariableDefinitions;

    public static TargetVariableDefinition GetDefinition(TargetVariableId id)
    {
        int index = (int)id;
        if ((uint)index >= (uint)VariableDefinitions.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown target variable identifier.");
        }

        return VariableDefinitions[index];
    }
}
