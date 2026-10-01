using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using CosmosDbQueryTool.Models;
using CosmosDbQueryTool.Services;

namespace CosmosDbQueryTool;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private const string DefaultDetailsMessage = "Run a query and select a row to inspect the full JSON payload here.";
    private const string DefaultQueryText = "SELECT TOP 25 * FROM c where c.id = \"\"";
    private const string DefaultEndpoint = "https://cs-diagnosticeventservice.documents.azure.com:443/";
    private const string DefaultKey = "";
    private const string DefaultDatabase = "diag-event-db";
    private const string DefaultContainer = "diag-event-container";
    private readonly CosmosQueryService _queryService = new();
    private readonly AppStateStore _appStateStore = new();
    private bool _isBusy;
    private ConnectionTabViewModel? _selectedConnectionSubscription;
    private ConnectionWindow? _connectionWindow;
    private CosmosQueryDocument? _selectedResult;
    private string _selectedDocumentJson = DefaultDetailsMessage;
    private string _statusMessage = "Ready. Open Connections to select a connection and run a query.";
    private ConnectionTabViewModel? _selectedConnectionTab;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        Loaded += MainWindow_OnLoaded;
        Closing += MainWindow_OnClosing;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<CosmosQueryDocument> Results { get; } = [];

    public ObservableCollection<ConnectionTabViewModel> ConnectionTabs { get; } = [];

    public ObservableCollection<string> RecentQueries { get; } = [];

    public ConnectionTabViewModel? SelectedConnectionTab
    {
        get => _selectedConnectionTab;
        set
        {
            if (_selectedConnectionTab == value)
            {
                return;
            }

            _selectedConnectionTab = value;
            UpdateSelectedConnectionSubscription(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentConnectionName));
        }
    }

    public string CurrentConnectionName => SelectedConnectionTab?.DisplayName ?? "No connection selected";

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value)
            {
                return;
            }

            _isBusy = value;
            OnPropertyChanged();
        }
    }

    public CosmosQueryDocument? SelectedResult
    {
        get => _selectedResult;
        set
        {
            if (_selectedResult == value)
            {
                return;
            }

            _selectedResult = value;
            SelectedDocumentJson = value?.Json ?? DefaultDetailsMessage;
            OnPropertyChanged();
        }
    }

    public string SelectedDocumentJson
    {
        get => _selectedDocumentJson;
        private set
        {
            if (_selectedDocumentJson == value)
            {
                return;
            }

            _selectedDocumentJson = value;
            OnPropertyChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage == value)
            {
                return;
            }

            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadApplicationState();
    }

    private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        TrySaveApplicationState();
    }

    private void OpenConnectionsButton_OnClick(object sender, RoutedEventArgs e)
    {
        OpenConnectionWindow();
    }

    public void OpenConnectionWindow()
    {
        if (_connectionWindow is null)
        {
            _connectionWindow = new ConnectionWindow();
            _connectionWindow.InitializeFor(this);
            _connectionWindow.Closed += ConnectionWindow_OnClosed;
            _connectionWindow.Show();
            return;
        }

        if (!_connectionWindow.IsVisible)
        {
            _connectionWindow.Show();
        }

        if (_connectionWindow.WindowState == WindowState.Minimized)
        {
            _connectionWindow.WindowState = WindowState.Normal;
        }

        _connectionWindow.Activate();
        _connectionWindow.Topmost = true;
        _connectionWindow.Topmost = false;
        _connectionWindow.Focus();
    }

    public void AddConnectionTab()
    {
        var connectionTab = CreateDefaultConnectionTab(ConnectionTabs.Count + 1);
        ConnectionTabs.Add(connectionTab);
        SelectedConnectionTab = connectionTab;
        StatusMessage = $"Added '{connectionTab.DisplayName}'. Enter connection details and run a query when ready.";
    }

    private void ConnectionWindow_OnClosed(object? sender, EventArgs e)
    {
        if (sender is ConnectionWindow closedWindow && ReferenceEquals(closedWindow, _connectionWindow))
        {
            _connectionWindow.Closed -= ConnectionWindow_OnClosed;
            _connectionWindow = null;
        }
    }

    private async void RunQueryButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (IsBusy)
        {
            return;
        }

        var connectionTab = SelectedConnectionTab;
        if (connectionTab is null)
        {
            const string selectedConnectionMessage = "Please select a connection tab before running a query.";
            MessageBox.Show(this, selectedConnectionMessage, "No connection selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            StatusMessage = selectedConnectionMessage;
            return;
        }

        if (!TryBuildConnectionSettings(connectionTab, out var connectionSettings, out var validationMessage))
        {
            MessageBox.Show(this, validationMessage, "Missing required input", MessageBoxButton.OK, MessageBoxImage.Warning);
            StatusMessage = validationMessage;
            return;
        }

        if (!int.TryParse(MaxItemsTextBox.Text.Trim(), out var maxItems) || maxItems <= 0)
        {
            const string maxItemsMessage = "Max Items must be a positive whole number.";
            MessageBox.Show(this, maxItemsMessage, "Invalid Max Items", MessageBoxButton.OK, MessageBoxImage.Warning);
            StatusMessage = maxItemsMessage;
            return;
        }

        var queryText = QueryTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(queryText))
        {
            const string queryMessage = "Please enter a Cosmos DB SQL query before running.";
            MessageBox.Show(this, queryMessage, "Missing query", MessageBoxButton.OK, MessageBoxImage.Warning);
            StatusMessage = queryMessage;
            return;
        }

        try
        {
            SetBusyState(true);
            StatusMessage = $"Running query against Cosmos DB using '{connectionTab.DisplayName}'...";
            Results.Clear();
            SelectedResult = null;
            SelectedDocumentJson = "Loading query results...";

            var documents = await _queryService.ExecuteQueryAsync(connectionSettings, queryText, maxItems);

            SaveSuccessfulConnection(connectionTab);

            foreach (var document in documents)
            {
                Results.Add(document);
            }

            if (Results.Count > 0)
            {
                SelectedResult = Results[0];
                StatusMessage = $"Loaded {Results.Count} result(s) from '{connectionSettings.ContainerName}'. Select a row to inspect its JSON.";
                AddRecentQuery(queryText, ConnectionTabs.IndexOf(connectionTab));
            }
            else
            {
                SelectedDocumentJson = "The query completed successfully, but it returned no rows.";
                StatusMessage = "Query completed successfully with no rows returned.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Query failed. Review the error and adjust the connection or SQL query.";
            SelectedDocumentJson = $"Error\n-----\n{ex.Message}";
            MessageBox.Show(this, ex.Message, "Cosmos DB query failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusyState(false);
        }
    }

    private void ClearResultsButton_OnClick(object sender, RoutedEventArgs e)
    {
        Results.Clear();
        SelectedResult = null;
        SelectedDocumentJson = DefaultDetailsMessage;
        StatusMessage = "Results cleared. Update your query and run it again when ready.";
    }

    private void RecentQueriesComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox || comboBox.SelectedItem is not string queryText)
        {
            return;
        }

        QueryTextBox.Text = queryText;
        QueryTextBox.CaretIndex = QueryTextBox.Text.Length;
    }

    private bool TryBuildConnectionSettings(ConnectionTabViewModel connectionTab, out CosmosConnectionSettings connectionSettings, out string validationMessage)
    {
        var endpoint = connectionTab.Endpoint.Trim();
        var key = connectionTab.Key.Trim();
        var databaseName = connectionTab.DatabaseName.Trim();
        var containerName = connectionTab.ContainerName.Trim();

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            connectionSettings = default!;
            validationMessage = "Endpoint is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            connectionSettings = default!;
            validationMessage = "Primary Key is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(databaseName))
        {
            connectionSettings = default!;
            validationMessage = "Database is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(containerName))
        {
            connectionSettings = default!;
            validationMessage = "Container is required.";
            return false;
        }

        connectionSettings = new CosmosConnectionSettings(endpoint, key, databaseName, containerName);
        validationMessage = string.Empty;
        return true;
    }

    private void LoadApplicationState()
    {
        var state = _appStateStore.LoadState();

        ConnectionTabs.Clear();
        RecentQueries.Clear();

        if (state.Connections.Count == 0)
        {
            ConnectionTabs.Add(CreateDefaultConnectionTab());
        }
        else
        {
            foreach (var savedConnection in state.Connections)
            {
                ConnectionTabs.Add(ToConnectionTab(savedConnection));
            }
        }

        foreach (var recentQuery in state.RecentQueries.Where(query => !string.IsNullOrWhiteSpace(query)))
        {
            if (!RecentQueries.Contains(recentQuery))
            {
                RecentQueries.Add(recentQuery);
            }
        }

        SelectedConnectionTab = ConnectionTabs.Count > 0
            ? ConnectionTabs[Math.Clamp(state.SelectedConnectionIndex, 0, ConnectionTabs.Count - 1)]
            : null;

        if (SelectedConnectionTab is null && ConnectionTabs.Count > 0)
        {
            SelectedConnectionTab = ConnectionTabs[0];
        }

        if (string.IsNullOrWhiteSpace(QueryTextBox.Text))
        {
            QueryTextBox.Text = DefaultQueryText;
        }

        StatusMessage = ConnectionTabs.Count > 0
            ? "Ready. Open Connections to manage saved connection profiles, then run a query and reuse successful queries from the drop-down."
            : "Ready. Open Connections to add a connection and run a query.";
    }

    private void TrySaveApplicationState(int? selectedConnectionIndex = null)
    {
        try
        {
            _appStateStore.SaveState(BuildAppState(selectedConnectionIndex));
        }
        catch
        {
            // Best-effort persistence; the user can continue using the app even if saving fails.
        }
    }

    private AppState BuildAppState(int? selectedConnectionIndex = null)
    {
        return new AppState
        {
            Connections = ConnectionTabs.Select(tab => new ConnectionProfileState
            {
                Name = tab.Name,
                Endpoint = tab.Endpoint,
                ProtectedKey = AppStateStore.ProtectString(tab.Key),
                DatabaseName = tab.DatabaseName,
                ContainerName = tab.ContainerName
            }).ToList(),
            RecentQueries = RecentQueries.Take(50).ToList(),
            SelectedConnectionIndex = selectedConnectionIndex ?? (SelectedConnectionTab is null ? 0 : ConnectionTabs.IndexOf(SelectedConnectionTab))
        };
    }

    private void SaveSuccessfulConnection(ConnectionTabViewModel connectionTab)
    {
        try
        {
            _appStateStore.SaveState(BuildAppState(ConnectionTabs.IndexOf(connectionTab)));
        }
        catch
        {
            // Best-effort persistence; a connection success should never fail because saving failed.
        }
    }

    private void AddRecentQuery(string queryText, int? selectedConnectionIndex = null)
    {
        var normalizedQuery = queryText.Trim();
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return;
        }

        var existingIndex = RecentQueries.IndexOf(normalizedQuery);
        if (existingIndex >= 0)
        {
            RecentQueries.RemoveAt(existingIndex);
        }

        RecentQueries.Insert(0, normalizedQuery);

        while (RecentQueries.Count > 50)
        {
            RecentQueries.RemoveAt(RecentQueries.Count - 1);
        }

        TrySaveApplicationState(selectedConnectionIndex);
    }

    private static ConnectionTabViewModel CreateDefaultConnectionTab(int index = 1)
    {
        return new ConnectionTabViewModel
        {
            Name = $"Connection {index}",
            Endpoint = DefaultEndpoint,
            Key = DefaultKey,
            DatabaseName = DefaultDatabase,
            ContainerName = DefaultContainer
        };
    }

    private static ConnectionTabViewModel ToConnectionTab(ConnectionProfileState savedConnection)
    {
        return new ConnectionTabViewModel
        {
            Name = string.IsNullOrWhiteSpace(savedConnection.Name) ? "Saved Connection" : savedConnection.Name,
            Endpoint = savedConnection.Endpoint,
            Key = AppStateStore.UnprotectString(savedConnection.ProtectedKey),
            DatabaseName = savedConnection.DatabaseName,
            ContainerName = savedConnection.ContainerName
        };
    }

    private void SetBusyState(bool isBusy)
    {
        IsBusy = isBusy;
        RunQueryButton.IsEnabled = !isBusy;
        ClearResultsButton.IsEnabled = !isBusy;
        OpenConnectionsButton.IsEnabled = !isBusy;
    }

    private void UpdateSelectedConnectionSubscription(ConnectionTabViewModel? newSubscription)
    {
        if (_selectedConnectionSubscription is not null)
        {
            _selectedConnectionSubscription.PropertyChanged -= SelectedConnectionSubscription_OnPropertyChanged;
        }

        _selectedConnectionSubscription = newSubscription;

        if (_selectedConnectionSubscription is not null)
        {
            _selectedConnectionSubscription.PropertyChanged += SelectedConnectionSubscription_OnPropertyChanged;
        }
    }

    private void SelectedConnectionSubscription_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ConnectionTabViewModel.Name) || e.PropertyName == nameof(ConnectionTabViewModel.DisplayName))
        {
            OnPropertyChanged(nameof(CurrentConnectionName));
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}