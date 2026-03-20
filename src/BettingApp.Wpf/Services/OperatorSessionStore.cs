using System.IO;
using System.Text.Json;

namespace BettingApp.Wpf.Services;

public sealed class OperatorSessionStore(string sessionPath)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<OperatorSessionData?> LoadAsync()
    {
        if (!File.Exists(sessionPath))
        {
            return null;
        }

        await using var stream = File.OpenRead(sessionPath);
        return await JsonSerializer.DeserializeAsync<OperatorSessionData>(stream, SerializerOptions);
    }

    public async Task SaveAsync(OperatorSessionData session)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath)!);
        await using var stream = File.Create(sessionPath);
        await JsonSerializer.SerializeAsync(stream, session, SerializerOptions);
    }

    public Task ClearAsync()
    {
        if (File.Exists(sessionPath))
        {
            File.Delete(sessionPath);
        }

        return Task.CompletedTask;
    }
}

public sealed class OperatorSessionData
{
    public string AccessToken { get; set; } = string.Empty;

    public string? RefreshToken { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public List<string> Roles { get; set; } = [];
}
