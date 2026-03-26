namespace BettingApp.Server.Models;

public sealed record PlayerMarketSummaryResponse(
    Guid MarketId,
    string EventName,
    decimal CurrentOdds,
    bool IsActive,
    DateTime CreatedAtUtc);
