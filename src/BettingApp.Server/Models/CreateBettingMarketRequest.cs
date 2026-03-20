namespace BettingApp.Server.Models;

public sealed record CreateBettingMarketRequest(
    string EventName,
    decimal OpeningOdds,
    bool IsActive);
