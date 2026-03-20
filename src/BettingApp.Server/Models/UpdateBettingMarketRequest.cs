namespace BettingApp.Server.Models;

public sealed record UpdateBettingMarketRequest(
    string EventName,
    decimal OpeningOdds,
    bool IsActive);
