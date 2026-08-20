using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace KernelMemoryLab.Target;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly TargetMemoryBlock _targetMemory;
    private DateTimeOffset _lastRefreshTime;

    public MainWindowViewModel(TargetMemoryBlock targetMemory)
    {
        ArgumentNullException.ThrowIfNull(targetMemory);

        _targetMemory = targetMemory;
        using Process process = Process.GetCurrentProcess();
        ProcessName = $"{typeof(App).Assembly.GetName().Name}.exe";
        ProcessId = process.Id;
        BaseAddress = FormatAddress(targetMemory.BaseAddress);
        Variables = TargetMemoryLayout.Variables
            .Select(variable => new TargetVariableRowViewModel(variable, targetMemory.GetAddress(variable.Id)))
            .ToArray();

        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ProcessName { get; }

    public int ProcessId { get; }

    public string BaseAddress { get; }

    public IReadOnlyList<TargetVariableRowViewModel> Variables { get; }

    public DateTimeOffset LastRefreshTime
    {
        get => _lastRefreshTime;
        private set
        {
            if (_lastRefreshTime == value)
            {
                return;
            }

            _lastRefreshTime = value;
            OnPropertyChanged();
        }
    }

    public void Refresh()
    {
        IReadOnlyList<TargetVariableSnapshot> snapshots = _targetMemory.ReadAll();
        for (int index = 0; index < snapshots.Count; index++)
        {
            Variables[index].UpdateValue(snapshots[index]);
        }

        LastRefreshTime = DateTimeOffset.Now;
    }

    private static string FormatAddress(IntPtr address) =>
        $"0x{unchecked((ulong)address.ToInt64()):X16}";

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class TargetVariableRowViewModel : INotifyPropertyChanged
{
    private string _currentValue = string.Empty;

    public TargetVariableRowViewModel(TargetVariableDefinition definition, IntPtr address)
    {
        Definition = definition;
        Address = $"0x{unchecked((ulong)address.ToInt64()):X16}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public TargetVariableDefinition Definition { get; }

    public string Name => Definition.Name;

    public string Type => Definition.Type switch
    {
        TargetValueType.Signed32 => "Int32",
        TargetValueType.Signed64 => "Int64",
        TargetValueType.Real32 => "Float32",
        _ => throw new InvalidOperationException($"Unsupported target value type: {Definition.Type}."),
    };

    public string Address { get; }

    public string CurrentValue
    {
        get => _currentValue;
        private set
        {
            if (string.Equals(_currentValue, value, StringComparison.Ordinal))
            {
                return;
            }

            _currentValue = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentValue)));
        }
    }

    public void UpdateValue(TargetVariableSnapshot snapshot)
    {
        if (snapshot.Definition.Id != Definition.Id)
        {
            throw new ArgumentException("Snapshot does not match this variable row.", nameof(snapshot));
        }

        CurrentValue = snapshot.Definition.Type switch
        {
            TargetValueType.Signed32 => ((int)snapshot.Value).ToString(CultureInfo.InvariantCulture),
            TargetValueType.Signed64 => ((long)snapshot.Value).ToString(CultureInfo.InvariantCulture),
            TargetValueType.Real32 => ((float)snapshot.Value).ToString("0.0###", CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException(
                $"Unsupported target value type: {snapshot.Definition.Type}."),
        };
    }
}
