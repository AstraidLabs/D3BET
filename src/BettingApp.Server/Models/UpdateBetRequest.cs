namespace BettingApp.Server.Models;

public sealed record UpdateBetRequest(
    Guid MarketId,
    Guid? BettorId,
    string? BettorName,
    decimal Stake,
    bool IsCommissionFeePaid);
