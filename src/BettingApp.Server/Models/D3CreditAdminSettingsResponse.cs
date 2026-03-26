namespace BettingApp.Server.Models;

public sealed record D3CreditAdminSettingsResponse(
    string CreditCode,
    string BaseCurrencyCode,
    decimal BaseCreditsPerCurrencyUnit,
    decimal BaseCurrencyUnitsPerCredit,
    int LowParticipationThreshold,
    decimal LowParticipationBoostPercent,
    int HighParticipationThreshold,
    decimal HighParticipationReductionPercent,
    decimal TotalStakePressureDivisor,
    decimal MaxPressureReductionPercent,
    decimal OddsVolatilityWeightPercent,
    bool EnableTestTopUpGateway,
    bool EnableManualCreditAdjustments,
    bool EnableManualBetRefunds,
    decimal DefaultTopUpAmount,
    IReadOnlyList<D3CreditMarketAdminRuleResponse> MarketRules);
