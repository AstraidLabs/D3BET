namespace BettingApp.Server.Models;

public sealed record D3CreditQuoteRequest(
    decimal RealMoneyAmount = 0m,
    decimal CreditStake = 0m);
