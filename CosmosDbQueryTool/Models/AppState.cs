namespace CosmosDbQueryTool.Models;

public sealed class AppState
{
    public List<ConnectionProfileState> Connections { get; set; } = [];

    public List<string> RecentQueries { get; set; } = [];

    public int SelectedConnectionIndex { get; set; }
}

public sealed class ConnectionProfileState
{
    public string Name { get; set; } = string.Empty;

    public string Endpoint { get; set; } = string.Empty;

    public string ProtectedKey { get; set; } = string.Empty;

    public string DatabaseName { get; set; } = string.Empty;

    public string ContainerName { get; set; } = string.Empty;
}
