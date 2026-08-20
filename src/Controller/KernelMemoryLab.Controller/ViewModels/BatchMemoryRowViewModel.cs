using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KernelMemoryLab.Controller.ViewModels;

public sealed class BatchMemoryRowViewModel : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _address = string.Empty;
    private string _type = "Int32";
    private string _readValue = string.Empty;
    private string _writeValue = string.Empty;
    private string _status = "Not run";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name { get => _name; set => SetField(ref _name, value); }

    public string Address { get => _address; set => SetField(ref _address, value); }

    public string Type { get => _type; set => SetField(ref _type, value); }

    public string ReadValue { get => _readValue; set => SetField(ref _readValue, value); }

    public string WriteValue { get => _writeValue; set => SetField(ref _writeValue, value); }

    public string Status { get => _status; set => SetField(ref _status, value); }

    private void SetField(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        if (string.Equals(field, value, StringComparison.Ordinal))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
