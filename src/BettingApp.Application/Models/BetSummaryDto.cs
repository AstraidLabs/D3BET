namespace BettingApp.Application.Models;

using BettingApp.Domain.Entities;

public sealed record BetSummaryDto(
    Guid Id,
    Guid BettorId,
    Guid? BettingMarketId,
    string EventName,
    decimal Odds,
    decimal Stake,
    string StakeCurrencyCode,
    decimal StakeRealMoneyEquivalent,
    bool IsWinning,
    BetOutcomeStatus OutcomeStatus,
    bool IsCommissionFeePaid,
    decimal PotentialPayout,
    string BettorName,
    DateTime PlacedAtLocal);
