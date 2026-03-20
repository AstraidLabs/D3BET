using MediatR;

namespace BettingApp.Application.Bets.Commands;

public sealed record CreateBetCommand(
    Guid MarketId,
    Guid? BettorId,
    string? BettorName,
    decimal Stake,
    bool IsCommissionFeePaid) : IRequest<decimal>;
