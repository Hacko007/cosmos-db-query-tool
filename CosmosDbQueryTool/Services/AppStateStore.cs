using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CosmosDbQueryTool.Models;

namespace CosmosDbQueryTool.Services;

public sealed class AppStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _stateFilePath;

    public AppStateStore()
    {
        var appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CosmosDbQueryTool");

        Directory.CreateDirectory(appDataFolder);
        _stateFilePath = Path.Combine(appDataFolder, "app-state.json");
    }

    public AppState LoadState()
    {
        if (!File.Exists(_stateFilePath))
        {
            return new AppState();
        }

        try
        {
            using var stream = File.OpenRead(_stateFilePath);
            return JsonSerializer.Deserialize<AppState>(stream, JsonOptions) ?? new AppState();
        }
        catch
        {
            return new AppState();
        }
    }

    public void SaveState(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var json = JsonSerializer.Serialize(state, JsonOptions);
        File.WriteAllText(_stateFilePath, json);
    }

    public static string ProtectString(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var bytes = Encoding.UTF8.GetBytes(value);
        var protectedBytes = ProtectedData.Protect(bytes, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    public static string UnprotectString(string protectedValue)
    {
        if (string.IsNullOrWhiteSpace(protectedValue))
        {
            return string.Empty;
        }

        try
        {
            var protectedBytes = Convert.FromBase64String(protectedValue);
            var bytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return string.Empty;
        }
    }
}

