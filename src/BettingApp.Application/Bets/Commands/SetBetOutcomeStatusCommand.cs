using BettingApp.Domain.Entities;
using MediatR;

namespace BettingApp.Application.Bets.Commands;

public sealed record SetBetOutcomeStatusCommand(Guid BetId, BetOutcomeStatus OutcomeStatus) : IRequest;
