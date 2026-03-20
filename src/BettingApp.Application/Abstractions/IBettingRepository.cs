using BettingApp.Application.Models;
using BettingApp.Domain.Entities;

namespace BettingApp.Application.Abstractions;

public interface IBettingRepository
{
    Task<IReadOnlyList<BettorListItem>> GetBettorsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<BettingMarketListItem>> GetBettingMarketsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<BetSummaryDto>> GetRecentBetsAsync(CancellationToken cancellationToken);

    Task<Bettor> GetOrCreateBettorAsync(Guid? bettorId, string? bettorName, CancellationToken cancellationToken);

    Task<int> CountBetsForEventAsync(string eventName, Guid? excludeBetId, CancellationToken cancellationToken);

    Task<EventBettingLoadDto> GetMarketBettingLoadAsync(Guid marketId, Guid? excludeBetId, CancellationToken cancellationToken);

    Task<BettingMarket> GetBettingMarketAsync(Guid marketId, CancellationToken cancellationToken);

    Task AddBettingMarketAsync(BettingMarket market, CancellationToken cancellationToken);

    Task UpdateBettingMarketAsync(Guid marketId, string eventName, decimal openingOdds, bool isActive, CancellationToken cancellationToken);

    Task RecalculateMarketCurrentOddsAsync(Guid marketId, CancellationToken cancellationToken);

    Task AddBetAsync(Bet bet, CancellationToken cancellationToken);

    Task DeleteBetAsync(Guid betId, CancellationToken cancellationToken);

    Task<Bet> GetBetAsync(Guid betId, CancellationToken cancellationToken);

    Task UpdateBetAsync(
        Guid betId,
        Guid marketId,
        Guid bettorId,
        string eventName,
        decimal odds,
        decimal stake,
        bool isCommissionFeePaid,
        CancellationToken cancellationToken);

    Task SetBetOutcomeStatusAsync(Guid betId, BetOutcomeStatus outcomeStatus, CancellationToken cancellationToken);
}
