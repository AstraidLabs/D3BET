namespace BettingApp.Server.Models;

public sealed record D3CreditManualAdjustmentRequest(
    Guid? BettorId,
    string? BettorName,
    decimal CreditAmount,
    decimal? RealMoneyAmount,
    string? CurrencyCode,
    string Reason,
    string? Reference);
