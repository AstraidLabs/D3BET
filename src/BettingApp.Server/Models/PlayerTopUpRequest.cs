namespace BettingApp.Server.Models;

public sealed record PlayerTopUpRequest(
    decimal RealMoneyAmount,
    string? CurrencyCode);
