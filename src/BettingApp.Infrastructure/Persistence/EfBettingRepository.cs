using BettingApp.Application.Abstractions;
using BettingApp.Application.Models;
using BettingApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BettingApp.Infrastructure.Persistence;

public sealed class EfBettingRepository(BettingDbContext dbContext) : IBettingRepository
{
    public async Task<IReadOnlyList<BettorListItem>> GetBettorsAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Bettors
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new BettorListItem(x.Id, x.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BettingMarketListItem>> GetBettingMarketsAsync(CancellationToken cancellationToken)
    {
        return await dbContext.BettingMarkets
            .AsNoTracking()
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Select(x => new BettingMarketListItem(
                x.Id,
                x.EventName,
                x.OpeningOdds,
                x.CurrentOdds,
                x.IsActive,
                x.CreatedAtUtc.ToLocalTime()))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BetSummaryDto>> GetRecentBetsAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Bets
            .AsNoTracking()
            .Include(x => x.Bettor)
            .OrderByDescending(x => x.PlacedAtUtc)
            .Take(20)
            .Select(x => new BetSummaryDto(
                x.Id,
                x.BettorId,
                x.BettingMarketId,
                x.EventName,
                x.Odds,
                x.Stake,
                x.StakeCurrencyCode,
                x.StakeRealMoneyEquivalent,
                x.IsWinning,
                x.OutcomeStatus,
                x.IsCommissionFeePaid,
                Math.Round(x.Odds * x.Stake, 2, MidpointRounding.AwayFromZero),
                x.Bettor!.Name,
                x.PlacedAtUtc.ToLocalTime()))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountBetsForEventAsync(string eventName, Guid? excludeBetId, CancellationToken cancellationToken)
    {
        var normalizedEventName = eventName.Trim().ToLowerInvariant();

        return await dbContext.Bets
            .AsNoTracking()
            .CountAsync(
                x => x.EventName.ToLower() == normalizedEventName
                    && (!excludeBetId.HasValue || x.Id != excludeBetId.Value),
                cancellationToken);
    }

    public async Task<EventBettingLoadDto> GetMarketBettingLoadAsync(Guid marketId, Guid? excludeBetId, CancellationToken cancellationToken)
    {
        var eventBets = dbContext.Bets
            .AsNoTracking()
            .Where(x => x.BettingMarketId == marketId
                && (!excludeBetId.HasValue || x.Id != excludeBetId.Value));

        var betCount = await eventBets.CountAsync(cancellationToken);
        var uniqueBettorCount = await eventBets
            .Select(x => x.BettorId)
            .Distinct()
            .CountAsync(cancellationToken);
        var totalStake = await eventBets.SumAsync(x => (decimal?)x.Stake, cancellationToken) ?? 0m;

        return new EventBettingLoadDto(betCount, uniqueBettorCount, totalStake);
    }

    public async Task<BettingMarket> GetBettingMarketAsync(Guid marketId, CancellationToken cancellationToken)
    {
        var market = await dbContext.BettingMarkets.FirstOrDefaultAsync(x => x.Id == marketId, cancellationToken);
        if (market is null)
        {
            throw new InvalidOperationException("Vypsaná událost nebyla nalezena.");
        }

        return market;
    }

    public async Task AddBettingMarketAsync(BettingMarket market, CancellationToken cancellationToken)
    {
        dbContext.BettingMarkets.Add(market);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateBettingMarketAsync(Guid marketId, string eventName, decimal openingOdds, bool isActive, CancellationToken cancellationToken)
    {
        var market = await dbContext.BettingMarkets.FirstOrDefaultAsync(x => x.Id == marketId, cancellationToken);
        if (market is null)
        {
            throw new InvalidOperationException("Vypsaná událost nebyla nalezena.");
        }

        market.EventName = eventName;
        market.OpeningOdds = openingOdds;
        market.IsActive = isActive;

        await dbContext.SaveChangesAsync(cancellationToken);
        await RecalculateMarketCurrentOddsAsync(marketId, cancellationToken);
    }

    public async Task RecalculateMarketCurrentOddsAsync(Guid marketId, CancellationToken cancellationToken)
    {
        var market = await dbContext.BettingMarkets.FirstOrDefaultAsync(x => x.Id == marketId, cancellationToken);
        if (market is null)
        {
            return;
        }

        var load = await GetMarketBettingLoadAsync(marketId, null, cancellationToken);
        market.CurrentOdds = Application.Services.DynamicOddsCalculator.CalculateAdjustedOdds(
            market.OpeningOdds,
            load.BetCount,
            load.UniqueBettorCount,
            load.TotalStake);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Bettor> GetOrCreateBettorAsync(Guid? bettorId, string? bettorName, CancellationToken cancellationToken)
    {
        if (bettorId.HasValue && bettorId.Value != Guid.Empty)
        {
            var existingBettor = await dbContext.Bettors.FirstOrDefaultAsync(x => x.Id == bettorId.Value, cancellationToken);
            if (existingBettor is not null)
            {
                return existingBettor;
            }
        }

        var normalizedName = bettorName?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new InvalidOperationException("Sazejici nebyl nalezen.");
        }

        var loweredName = normalizedName.ToLowerInvariant();
        var bettor = await dbContext.Bettors.FirstOrDefaultAsync(
            x => x.Name.ToLower() == loweredName,
            cancellationToken);

        if (bettor is not null)
        {
            return bettor;
        }

        bettor = new Bettor
        {
            Name = normalizedName
        };

        dbContext.Bettors.Add(bettor);
        await dbContext.SaveChangesAsync(cancellationToken);
        return bettor;
    }

    public async Task AddBetAsync(Bet bet, CancellationToken cancellationToken)
    {
        dbContext.Bets.Add(bet);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteBetAsync(Guid betId, CancellationToken cancellationToken)
    {
        var bet = await dbContext.Bets.FirstOrDefaultAsync(x => x.Id == betId, cancellationToken);
        if (bet is null)
        {
            throw new InvalidOperationException("Sazka nebyla nalezena.");
        }

        dbContext.Bets.Remove(bet);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (bet.BettingMarketId.HasValue)
        {
            await RecalculateMarketCurrentOddsAsync(bet.BettingMarketId.Value, cancellationToken);
        }
    }

    public async Task<Bet> GetBetAsync(Guid betId, CancellationToken cancellationToken)
    {
        var bet = await dbContext.Bets.FirstOrDefaultAsync(x => x.Id == betId, cancellationToken);
        if (bet is null)
        {
            throw new InvalidOperationException("Sazka nebyla nalezena.");
        }

        return bet;
    }

    public async Task UpdateBetAsync(
        Guid betId,
        Guid marketId,
        Guid bettorId,
        string eventName,
        decimal odds,
        decimal stake,
        bool isCommissionFeePaid,
        CancellationToken cancellationToken)
    {
        var bet = await dbContext.Bets.FirstOrDefaultAsync(x => x.Id == betId, cancellationToken);
        if (bet is null)
        {
            throw new InvalidOperationException("Sazka nebyla nalezena.");
        }

        bet.BettingMarketId = marketId;
        bet.BettorId = bettorId;
        bet.EventName = eventName;
        bet.Odds = odds;
        bet.Stake = stake;
        bet.IsCommissionFeePaid = isCommissionFeePaid;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetBetOutcomeStatusAsync(Guid betId, BetOutcomeStatus outcomeStatus, CancellationToken cancellationToken)
    {
        var bet = await dbContext.Bets.FirstOrDefaultAsync(x => x.Id == betId, cancellationToken);
        if (bet is null)
        {
            throw new InvalidOperationException("Sazka nebyla nalezena.");
        }

        bet.OutcomeStatus = outcomeStatus;
        bet.IsWinning = outcomeStatus == BetOutcomeStatus.Won;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
