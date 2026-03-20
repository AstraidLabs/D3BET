namespace BettingApp.Server.Models;

public sealed record CreateBetRequest(
    Guid MarketId,
    Guid? BettorId,
    string? BettorName,
    decimal Stake,
    bool IsCommissionFeePaid);
