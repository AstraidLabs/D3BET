using System.Text.Json;

namespace BettingApp.Server.Services;

public sealed class LicenseStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string licensesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BettingApp",
        "server-licenses.json");

    private readonly string serverIdentityPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BettingApp",
        "server-license.identity");

    public async Task<LicenseStoreState> LoadAsync(CancellationToken cancellationToken)
    {
        var serverInstanceId = await GetOrCreateServerInstanceIdAsync(cancellationToken);
        if (!File.Exists(licensesPath))
        {
            return new LicenseStoreState(serverInstanceId, []);
        }

        await using var stream = File.OpenRead(licensesPath);
        var state = await JsonSerializer.DeserializeAsync<LicenseStoreState>(stream, SerializerOptions, cancellationToken);
        return state is null
            ? new LicenseStoreState(serverInstanceId, [])
            : state with { ServerInstanceId = serverInstanceId };
    }

    public async Task SaveAsync(LicenseStoreState state, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(licensesPath)!);
        await using var stream = File.Create(licensesPath);
        await JsonSerializer.SerializeAsync(stream, state, SerializerOptions, cancellationToken);
    }

    private async Task<string> GetOrCreateServerInstanceIdAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(serverIdentityPath))
        {
            return (await File.ReadAllTextAsync(serverIdentityPath, cancellationToken)).Trim();
        }

        var serverInstanceId = $"SRV-{Guid.NewGuid():N}";
        Directory.CreateDirectory(Path.GetDirectoryName(serverIdentityPath)!);
        await File.WriteAllTextAsync(serverIdentityPath, serverInstanceId, cancellationToken);
        return serverInstanceId;
    }
}

public sealed record LicenseStoreState(
    string ServerInstanceId,
    IReadOnlyList<LicenseBindingRecord> Licenses,
    IReadOnlyList<LicenseAuditEntryRecord>? AuditEntries = null);

public sealed record LicenseBindingRecord(
    string LicenseId,
    string Email,
    string CustomerName,
    string ActivationKeyHash,
    string InstallationId,
    string MachineFingerprintHash,
    DateTime IssuedAtUtc,
    DateTime ExpiresAtUtc,
    bool IsRevoked,
    DateTime? LastValidatedAtUtc = null);

public sealed record LicenseAuditEntryRecord(
    Guid Id,
    DateTime CreatedAtUtc,
    string LicenseId,
    string EventType,
    string DisplayMessage,
    string Email,
    string InstallationId,
    bool IsSuccessful);
