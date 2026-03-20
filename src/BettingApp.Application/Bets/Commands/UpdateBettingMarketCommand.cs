using MediatR;

namespace BettingApp.Application.Bets.Commands;

public sealed record UpdateBettingMarketCommand(
    Guid MarketId,
    string EventName,
    decimal OpeningOdds,
    bool IsActive) : IRequest;
