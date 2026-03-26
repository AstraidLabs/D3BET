using BettingApp.Domain.Entities;

namespace BettingApp.Server.Models;

public sealed record D3CreditAdminTransactionResponse(
    Guid Id,
    Guid BettorId,
    string BettorName,
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
