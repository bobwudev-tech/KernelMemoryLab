using System.Windows;
using System.Windows.Threading;

namespace KernelMemoryLab.Target;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _refreshTimer;

    public MainWindow(TargetMemoryBlock targetMemory)
    {
        InitializeComponent();

        ViewModel = new MainWindowViewModel(targetMemory);
        DataContext = ViewModel;

        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _refreshTimer.Tick += RefreshTimerOnTick;
        _refreshTimer.Start();

        Closed += OnClosed;
    }

    public MainWindowViewModel ViewModel { get; }

    private void RefreshTimerOnTick(object? sender, EventArgs e) => ViewModel.Refresh();

    private void OnClosed(object? sender, EventArgs e)
    {
        _refreshTimer.Stop();
        _refreshTimer.Tick -= RefreshTimerOnTick;
        Closed -= OnClosed;
    }
}

