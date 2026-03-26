namespace BettingApp.Server.Models;

public sealed record D3CreditMarketAdminRuleResponse(
    Guid MarketId,
    bool IsEnabled,
    decimal AdditionalMultiplierPercent,
    decimal? OverrideMoneyToCreditRate,
    decimal? OverrideCreditToMoneyRate,
    string? Note);
