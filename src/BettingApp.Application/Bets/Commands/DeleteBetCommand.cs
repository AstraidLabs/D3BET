using MediatR;

namespace BettingApp.Application.Bets.Commands;

public sealed record DeleteBetCommand(Guid BetId) : IRequest;
