namespace BettingApp.Server.Models;

public sealed record PlayerWithdrawalRequest(
    decimal CreditAmount,
    string CurrencyCode,
    string? Reason);
