using BettingApp.Domain.Entities;

namespace BettingApp.Server.Models;

public sealed record SetBetOutcomeStatusRequest(BetOutcomeStatus OutcomeStatus);
