namespace BettingApp.Server.Models;

public sealed record D3CreditWalletResponse(
    Guid BettorId,
    string BettorName,
    string CreditCode,
    decimal Balance,
    decimal LastMoneyToCreditRate,
    decimal LastCreditToMoneyRate,
    IReadOnlyList<D3CreditTransactionResponse> Transactions);
