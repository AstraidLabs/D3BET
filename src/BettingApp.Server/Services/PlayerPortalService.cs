using BettingApp.Domain.Entities;
using BettingApp.Infrastructure.Persistence;
using BettingApp.Server.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BettingApp.Server.Services;

public sealed class PlayerPortalService(
    UserManager<IdentityUser> userManager,
    BettingDbContext dbContext,
    D3CreditService d3CreditService)
{
    public async Task<PlayerDashboardResponse> GetDashboardAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Přihlášený hráč nebyl nalezen.");

        var roles = await userManager.GetRolesAsync(user);
        var bettor = await GetOrCreateBettorAsync(user, cancellationToken);
        var wallet = await d3CreditService.GetWalletAsync(bettor.Id, cancellationToken);

        var markets = await dbContext.BettingMarkets
            .AsNoTracking()
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.EventName)
            .Select(x => new PlayerMarketSummaryResponse(
                x.Id,
                x.EventName,
                x.CurrentOdds,
                x.IsActive,
                x.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var recentBets = await dbContext.Bets
            .AsNoTracking()
            .Where(x => x.BettorId == bettor.Id)
            .OrderByDescending(x => x.PlacedAtUtc)
            .Take(20)
            .Select(x => new PlayerBetSummaryResponse(
                x.Id,
                x.BettingMarketId,
                x.EventName,
                x.Odds,
                x.Stake,
                x.StakeCurrencyCode,
                x.StakeRealMoneyEquivalent,
                x.PotentialPayout,
                x.OutcomeStatus,
                x.PlacedAtUtc))
            .ToListAsync(cancellationToken);

        var recentWithdrawals = await d3CreditService.GetRecentWithdrawalsAsync(bettor.Id, 20, cancellationToken);
        var recentReceipts = await d3CreditService.GetRecentReceiptsAsync(bettor.Id, 20, cancellationToken);

        return new PlayerDashboardResponse(
            new AccountProfileResponse(user.Id, user.UserName ?? user.Id, user.Email, user.EmailConfirmed, roles.ToArray()),
            wallet,
            markets,
            recentBets,
            recentWithdrawals,
            recentReceipts);
    }

    public async Task<D3CreditTopUpResponse> TopUpAsync(string userId, PlayerTopUpRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Přihlášený hráč nebyl nalezen.");

        return await d3CreditService.TopUpAsync(new D3CreditTopUpRequest(
            BettorId: null,
            BettorName: user.UserName,
            RealMoneyAmount: request.RealMoneyAmount,
            CurrencyCode: request.CurrencyCode,
            Reference: $"PLAYER-{user.Id[..8]}-{DateTime.UtcNow:yyyyMMddHHmmss}"), cancellationToken);
    }

    public async Task<D3CreditQuoteResponse> QuoteAsync(string userId, Guid marketId, PlayerCreditBetRequest request, CancellationToken cancellationToken)
    {
        await EnsureUserExistsAsync(userId);
        return await d3CreditService.QuoteAsync(marketId, new D3CreditQuoteRequest(0m, request.CreditStake), cancellationToken);
    }

    public async Task<D3CreditBetPlacementResponse> PlaceBetAsync(string userId, Guid marketId, PlayerCreditBetRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Přihlášený hráč nebyl nalezen.");

        return await d3CreditService.PlaceCreditBetAsync(
            marketId,
            new D3CreditBetRequest(null, user.UserName, request.CreditStake),
            cancellationToken);
    }

    public async Task<CreditWithdrawalResponse> RequestWithdrawalAsync(string userId, PlayerWithdrawalRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Přihlášený hráč nebyl nalezen.");
        var bettor = await GetOrCreateBettorAsync(user, cancellationToken);

        return await d3CreditService.RequestWithdrawalAsync(
            bettor.Id,
            request.Reason,
            request.CurrencyCode,
            request.CreditAmount,
            cancellationToken);
    }

    private async Task EnsureUserExistsAsync(string userId)
    {
        if (await userManager.FindByIdAsync(userId) is null)
        {
            throw new InvalidOperationException("Přihlášený hráč nebyl nalezen.");
        }
    }

    private async Task<Bettor> GetOrCreateBettorAsync(IdentityUser user, CancellationToken cancellationToken)
    {
        var userName = string.IsNullOrWhiteSpace(user.UserName) ? user.Id : user.UserName;
        var loweredName = userName.ToLowerInvariant();
        var bettor = await dbContext.Bettors.FirstOrDefaultAsync(x => x.Name.ToLower() == loweredName, cancellationToken);
        if (bettor is not null)
        {
            return bettor;
        }

        bettor = new Bettor
        {
            Name = userName
        };

        dbContext.Bettors.Add(bettor);
        await dbContext.SaveChangesAsync(cancellationToken);
        return bettor;
    }
}
