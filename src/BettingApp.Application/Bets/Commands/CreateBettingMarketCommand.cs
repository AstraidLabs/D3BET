using MediatR;

namespace BettingApp.Application.Bets.Commands;

public sealed record CreateBettingMarketCommand(
    string EventName,
    decimal OpeningOdds,
    bool IsActive) : IRequest<Guid>;
