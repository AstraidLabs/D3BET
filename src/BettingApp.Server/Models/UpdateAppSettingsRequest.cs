namespace BettingApp.Server.Models;

public sealed record UpdateAppSettingsRequest(
    bool EnableAutoRefresh,
    int AutoRefreshIntervalSeconds,
    bool EnableRealtimeRefresh,
    bool EnableTicketAnimations,
    bool EnableOperatorCommission,
    string OperatorCommissionFormula,
    decimal OperatorCommissionRatePercent,
    decimal OperatorFlatFeePerBet);
