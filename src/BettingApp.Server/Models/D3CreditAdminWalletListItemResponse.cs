namespace BettingApp.Server.Models;

public sealed record D3CreditAdminWalletListItemResponse(
    Guid BettorId,
    string BettorName,
    string CreditCode,
    decimal Balance,
    decimal LastMoneyToCreditRate,
    decimal LastCreditToMoneyRate,
    DateTime UpdatedAtUtc);
