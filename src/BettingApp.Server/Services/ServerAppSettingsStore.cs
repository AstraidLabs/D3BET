using System.Text.Json;
using BettingApp.Server.Models;

namespace BettingApp.Server.Services;

public sealed class ServerAppSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BettingApp",
        "server-app-settings.json");

    public async Task<AppSettingsResponse> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(settingsPath))
        {
            return AppSettingsResponse.Default;
        }

        await using var stream = File.OpenRead(settingsPath);
        var settings = await JsonSerializer.DeserializeAsync<AppSettingsResponse>(stream, SerializerOptions, cancellationToken);
        return settings ?? AppSettingsResponse.Default;
    }

    public async Task SaveAsync(UpdateAppSettingsRequest request, CancellationToken cancellationToken)
    {
        var settings = new AppSettingsResponse(
            request.EnableAutoRefresh,
            request.AutoRefreshIntervalSeconds,
            request.EnableRealtimeRefresh,
            request.EnableTicketAnimations,
            request.EnableOperatorCommission,
            request.OperatorCommissionFormula,
            request.OperatorCommissionRatePercent,
            request.OperatorFlatFeePerBet);

        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        await using var stream = File.Create(settingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, cancellationToken);
    }
}
