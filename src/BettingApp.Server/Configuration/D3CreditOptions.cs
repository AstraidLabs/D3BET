namespace BettingApp.Server.Configuration;

public sealed class D3CreditOptions
{
    public const string SectionName = "D3Credit";

    public string CreditCode { get; set; } = "D3Kredit";

    public string BaseCurrencyCode { get; set; } = "CZK";

    public decimal BaseCreditsPerCurrencyUnit { get; set; } = 10m;

    public decimal BaseCurrencyUnitsPerCredit { get; set; } = 0.10m;

    public int LowParticipationThreshold { get; set; } = 3;

    public decimal LowParticipationBoostPercent { get; set; } = 20m;

    public int HighParticipationThreshold { get; set; } = 10;

    public decimal HighParticipationReductionPercent { get; set; } = 15m;

    public decimal TotalStakePressureDivisor { get; set; } = 500m;

    public decimal MaxPressureReductionPercent { get; set; } = 20m;

    public decimal OddsVolatilityWeightPercent { get; set; } = 5m;

    public bool EnableTestTopUpGateway { get; set; } = true;

    public bool EnablePlayerWithdrawals { get; set; } = true;

    public bool AutoApproveWithdrawals { get; set; } = false;

    public bool AutoPayoutWinningBets { get; set; } = true;
}
