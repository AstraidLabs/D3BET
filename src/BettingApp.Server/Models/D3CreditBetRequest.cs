namespace BettingApp.Server.Models;

public sealed record D3CreditBetRequest(
    Guid? BettorId,
    string? BettorName,
    decimal CreditStake);
