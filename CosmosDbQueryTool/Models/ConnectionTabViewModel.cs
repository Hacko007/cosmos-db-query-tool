using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CosmosDbQueryTool.Models;

public sealed class ConnectionTabViewModel : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _endpoint = string.Empty;
    private string _key = string.Empty;
    private string _databaseName = string.Empty;
    private string _containerName = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name
    {
        get => _name;
        set
        {
            if (_name == value)
            {
                return;
            }

            _name = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "New Connection" : Name;

    public string Endpoint
    {
        get => _endpoint;
        set
        {
            if (_endpoint == value)
            {
                return;
            }

            _endpoint = value;
            OnPropertyChanged();
        }
    }

    public string Key
    {
        get => _key;
        set
        {
            if (_key == value)
            {
                return;
            }

            _key = value;
            OnPropertyChanged();
        }
    }

    public string DatabaseName
    {
        get => _databaseName;
        set
        {
            if (_databaseName == value)
            {
                return;
            }

            _databaseName = value;
            OnPropertyChanged();
        }
    }

    public string ContainerName
    {
        get => _containerName;
        set
        {
            if (_containerName == value)
            {
                return;
            }

            _containerName = value;
            OnPropertyChanged();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
