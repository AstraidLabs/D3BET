using System.IO;
using System.Text.Json;

namespace BettingApp.Wpf.Services;

public sealed class AppSettingsStore(string settingsPath)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public async Task<AppSettings> LoadAsync()
    {
        if (!File.Exists(settingsPath))
        {
            return AppSettings.Default;
        }

        await using var stream = File.OpenRead(settingsPath);
        var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, SerializerOptions);
        return settings ?? AppSettings.Default;
    }

    public async Task SaveAsync(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(settingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions);
    }
}

public sealed record AppSettings(
    bool EnableAutoRefresh = true,
    int AutoRefreshIntervalSeconds = 20,
    bool EnableRealtimeRefresh = true,
    bool EnableTicketAnimations = true,
    bool EnableOperatorCommission = true,
    string OperatorCommissionFormula = "PercentFromStake",
    decimal OperatorCommissionRatePercent = 5m,
    decimal OperatorFlatFeePerBet = 0m)
{
    public static AppSettings Default { get; } = new(
        EnableAutoRefresh: true,
        AutoRefreshIntervalSeconds: 20,
        EnableRealtimeRefresh: true,
        EnableTicketAnimations: true,
        EnableOperatorCommission: true,
        OperatorCommissionFormula: "PercentFromStake",
        OperatorCommissionRatePercent: 5m,
        OperatorFlatFeePerBet: 0m);
}
