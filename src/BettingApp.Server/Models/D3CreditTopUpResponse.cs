namespace BettingApp.Server.Models;

public sealed record D3CreditTopUpResponse(
    string PaymentGateway,
    string PaymentReference,
    string CreditCode,
    decimal NewBalance,
    decimal AddedCredits,
    decimal RealMoneyAmount,
    string CurrencyCode,
    decimal MoneyToCreditRate);
