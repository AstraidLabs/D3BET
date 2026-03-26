namespace BettingApp.Server.Models;

public sealed record D3CreditTopUpRequest(
    Guid? BettorId,
    string? BettorName,
    decimal RealMoneyAmount,
    string? CurrencyCode,
    string? Reference);
