using System.Diagnostics.CodeAnalysis;
using System.Windows;

namespace KernelMemoryLab.Target;

[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "WPF Application owns the block for its lifetime and releases it in OnExit.")]
public partial class App : Application
{
    private TargetMemoryBlock? _targetMemory;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _targetMemory = new TargetMemoryBlock();
        MainWindow = new MainWindow(_targetMemory);
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _targetMemory?.Dispose();
        _targetMemory = null;

        base.OnExit(e);
    }
}

