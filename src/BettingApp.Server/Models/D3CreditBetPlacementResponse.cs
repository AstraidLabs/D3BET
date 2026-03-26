namespace BettingApp.Server.Models;

public sealed record D3CreditBetPlacementResponse(
    decimal AppliedOdds,
    D3CreditWalletResponse Wallet,
    D3CreditQuoteResponse Quote);
