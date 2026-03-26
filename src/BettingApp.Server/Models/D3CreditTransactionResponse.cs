using BettingApp.Domain.Entities;

namespace BettingApp.Server.Models;

public sealed record D3CreditTransactionResponse(
    Guid Id,
    D3CreditTransactionType Type,
    decimal CreditAmount,
    decimal RealMoneyAmount,
    string RealCurrencyCode,
    decimal MoneyToCreditRate,
    decimal CreditToMoneyRate,
    decimal MarketParticipationMultiplier,
    string Reference,
    string Description,
    DateTime CreatedAtUtc);
