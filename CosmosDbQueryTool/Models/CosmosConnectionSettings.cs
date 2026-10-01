namespace CosmosDbQueryTool.Models;

public sealed record CosmosConnectionSettings(
    string Endpoint,
    string Key,
    string DatabaseName,
    string ContainerName);

