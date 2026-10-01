namespace CosmosDbQueryTool.Models;

public sealed record CosmosQueryDocument(
    int Index,
    string Id,
    string Preview,
    string Json);

