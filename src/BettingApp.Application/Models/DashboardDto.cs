namespace BettingApp.Application.Models;

public sealed record DashboardDto(
    IReadOnlyList<BettorListItem> Bettors,
    IReadOnlyList<BettingMarketListItem> Markets,
    IReadOnlyList<BetSummaryDto> RecentBets);
