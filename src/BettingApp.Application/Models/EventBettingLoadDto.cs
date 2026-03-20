namespace BettingApp.Application.Models;

public sealed record EventBettingLoadDto(
    int BetCount,
    int UniqueBettorCount,
    decimal TotalStake);
