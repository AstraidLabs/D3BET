using BettingApp.Domain.Entities;

namespace BettingApp.Server.Models;

public sealed record PlayerBetSummaryResponse(
    Guid BetId,
    Guid? MarketId,
    string EventName,
    decimal Odds,
    decimal Stake,
    string StakeCurrencyCode,
    decimal StakeRealMoneyEquivalent,
    decimal PotentialPayout,
    BetOutcomeStatus OutcomeStatus,
    DateTime PlacedAtUtc);
