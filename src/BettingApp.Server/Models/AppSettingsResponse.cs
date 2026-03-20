namespace BettingApp.Server.Models;

public sealed record AppSettingsResponse(
    bool EnableAutoRefresh = true,
    int AutoRefreshIntervalSeconds = 20,
    bool EnableRealtimeRefresh = true,
    bool EnableTicketAnimations = true,
    bool EnableOperatorCommission = true,
    string OperatorCommissionFormula = "PercentFromStake",
    decimal OperatorCommissionRatePercent = 5m,
    decimal OperatorFlatFeePerBet = 0m)
{
    public static AppSettingsResponse Default { get; } = new();
}
