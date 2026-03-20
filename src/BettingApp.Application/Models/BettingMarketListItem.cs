namespace BettingApp.Application.Models;

public sealed record BettingMarketListItem(
    Guid Id,
    string EventName,
    decimal OpeningOdds,
    decimal CurrentOdds,
    bool IsActive,
    DateTime CreatedAtLocal);
