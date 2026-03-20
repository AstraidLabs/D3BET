using MediatR;

namespace BettingApp.Application.Bets.Commands;

public sealed record UpdateBetCommand(
    Guid BetId,
    Guid MarketId,
    Guid? BettorId,
    string? BettorName,
    decimal Stake,
    bool IsCommissionFeePaid) : IRequest<decimal>;
