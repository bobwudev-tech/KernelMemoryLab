using System.Windows;
using KernelMemoryLab.Controller.ViewModels;

namespace KernelMemoryLab.Controller;

public partial class MainWindow : Window, IDisposable
{
    private readonly ControllerViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new ControllerViewModel();
        DataContext = _viewModel;
    }

    private void ConnectClick(object sender, RoutedEventArgs e) => _viewModel.Connect();

    private void DisconnectClick(object sender, RoutedEventArgs e) => _viewModel.Disconnect();

    private void ReadSingleClick(object sender, RoutedEventArgs e) => _viewModel.ReadSingle();

    private void WriteSingleClick(object sender, RoutedEventArgs e) => _viewModel.WriteSingle();

    private void ReadBatchClick(object sender, RoutedEventArgs e) => _viewModel.ReadBatch();

    private void WriteBatchClick(object sender, RoutedEventArgs e) => _viewModel.WriteBatch();

    protected override void OnClosed(EventArgs e)
    {
        Dispose();
        base.OnClosed(e);
    }

    public void Dispose()
    {
        _viewModel.Dispose();
        GC.SuppressFinalize(this);
    }
}

