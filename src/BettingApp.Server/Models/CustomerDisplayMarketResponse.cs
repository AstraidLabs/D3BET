namespace BettingApp.Server.Models;

public sealed record CustomerDisplayMarketResponse(
    Guid MarketId,
    string EventName,
    decimal CurrentOdds,
    decimal TotalStake,
    int TicketCount);
