namespace BettingApp.Server.Models;

public sealed record D3CreditQuoteResponse(
    Guid MarketId,
    string EventName,
    string CreditCode,
    string RealCurrencyCode,
    decimal MoneyToCreditRate,
    decimal CreditToMoneyRate,
    decimal MarketParticipationMultiplier,
    decimal RealMoneyAmount,
    decimal CreditAmount,
    decimal PotentialPayoutCredits,
    decimal PotentialPayoutRealMoney);
