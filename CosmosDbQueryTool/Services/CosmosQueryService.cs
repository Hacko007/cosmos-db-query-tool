using System.IO;
using System.Text.Json;
using CosmosDbQueryTool.Models;
using Microsoft.Azure.Cosmos;

namespace CosmosDbQueryTool.Services;

public sealed class CosmosQueryService
{
    private static readonly JsonSerializerOptions PrettyPrintJsonOptions = new()
    {
        WriteIndented = true
    };

    public async Task<IReadOnlyList<CosmosQueryDocument>> ExecuteQueryAsync(
        CosmosConnectionSettings connectionSettings,
        string queryText,
        int maxItems,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionSettings.Endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionSettings.Key);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionSettings.DatabaseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionSettings.ContainerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(queryText);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxItems, 0);

        var results = new List<CosmosQueryDocument>();
        var clientOptions = new CosmosClientOptions
        {
            ConnectionMode = ConnectionMode.Gateway
        };

        using var cosmosClient = new CosmosClient(connectionSettings.Endpoint, connectionSettings.Key, clientOptions);
        var container = cosmosClient.GetContainer(connectionSettings.DatabaseName, connectionSettings.ContainerName);
        var iterator = container.GetItemQueryStreamIterator(
            queryDefinition: new QueryDefinition(queryText),
            continuationToken: null,
            requestOptions: new QueryRequestOptions
            {
                MaxItemCount = Math.Min(maxItems, 100)
            });

        while (iterator.HasMoreResults && results.Count < maxItems)
        {
            using var response = await iterator.ReadNextAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var responseText = response.Content is null
                    ? string.Empty
                    : await new StreamReader(response.Content).ReadToEndAsync();

                throw new InvalidOperationException(
                    $"Cosmos DB returned {(int)response.StatusCode} ({response.StatusCode}). {response.ErrorMessage} {responseText}".Trim());
            }

            if (response.Content is null)
            {
                continue;
            }

            using var payload = await JsonDocument.ParseAsync(response.Content, cancellationToken: cancellationToken);
            if (!payload.RootElement.TryGetProperty("Documents", out var documents) || documents.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var document in documents.EnumerateArray())
            {
                results.Add(CreateResult(document, results.Count + 1));

                if (results.Count >= maxItems)
                {
                    break;
                }
            }
        }

        return results;
    }

    private static CosmosQueryDocument CreateResult(JsonElement document, int index)
    {
        var prettyJson = JsonSerializer.Serialize(document, PrettyPrintJsonOptions);
        var compactJson = JsonSerializer.Serialize(document);

        return new CosmosQueryDocument(
            index,
            ExtractId(document),
            BuildPreview(document, compactJson),
            prettyJson);
    }

    private static string ExtractId(JsonElement document)
    {
        if (document.ValueKind == JsonValueKind.Object &&
            document.TryGetProperty("id", out var idElement) &&
            idElement.ValueKind == JsonValueKind.String)
        {
            return idElement.GetString() ?? "(empty id)";
        }

        return document.ValueKind == JsonValueKind.Object ? "(no id)" : "(scalar result)";
    }

    private static string BuildPreview(JsonElement document, string compactJson)
    {
        if (document.ValueKind != JsonValueKind.Object)
        {
            return Truncate(compactJson, 160);
        }

        var parts = new List<string>();
        foreach (var property in document.EnumerateObject())
        {
            if (property.Name.StartsWith('_')|| property.Name.Equals("id", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var formattedValue = FormatPreviewValue(property.Value);
            if (formattedValue is null)
            {
                continue;
            }

            parts.Add($"{property.Name}: {formattedValue}");
            if (parts.Count == 3)
            {
                break;
            }
        }

        return parts.Count > 0
            ? Truncate(string.Join(" | ", parts), 160)
            : Truncate(compactJson, 160);
    }

    private static string? FormatPreviewValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => Truncate(value.GetString() ?? string.Empty, 50),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            JsonValueKind.Array => $"Array[{value.GetArrayLength()}]",
            JsonValueKind.Object => "{...}",
            _ => null
        };
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..(maxLength - 3)] + "...";
    }
}



