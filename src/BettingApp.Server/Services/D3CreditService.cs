using BettingApp.Application.Abstractions;
using BettingApp.Application.Models;
using BettingApp.Application.Services;
using BettingApp.Domain.Entities;
using BettingApp.Infrastructure.Persistence;
using BettingApp.Server.Configuration;
using BettingApp.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BettingApp.Server.Services;

public sealed class D3CreditService(
    BettingDbContext dbContext,
    IBettingRepository bettingRepository,
    IBettingNotifier bettingNotifier,
    ID3CreditPaymentGateway paymentGateway,
    D3CreditAdminSettingsStore adminSettingsStore,
    IOptions<D3CreditOptions> options)
{
    private readonly D3CreditOptions creditOptions = options.Value;

    public async Task<D3CreditWalletResponse> GetWalletAsync(Guid bettorId, CancellationToken cancellationToken)
    {
        var settings = await adminSettingsStore.LoadAsync(cancellationToken);
        var bettor = await dbContext.Bettors
            .AsNoTracking()
            .Include(x => x.Wallet)
            .Include(x => x.CreditTransactions.OrderByDescending(transaction => transaction.CreatedAtUtc).Take(25))
            .FirstOrDefaultAsync(x => x.Id == bettorId, cancellationToken)
            ?? throw new InvalidOperationException("Sázející nebyl nalezen.");

        var wallet = bettor.Wallet ?? new BettorWallet
        {
            BettorId = bettor.Id,
            CreditCode = settings.CreditCode
        };

        return MapWallet(bettor, wallet, bettor.CreditTransactions.OrderByDescending(x => x.CreatedAtUtc).Take(25).ToArray());
    }

    public async Task<D3CreditTopUpResponse> TopUpAsync(D3CreditTopUpRequest request, CancellationToken cancellationToken)
    {
        var settings = await adminSettingsStore.LoadAsync(cancellationToken);
        if (!settings.EnableTestTopUpGateway)
        {
            throw new InvalidOperationException("Testovací dobíjení je na serveru vypnuté.");
        }

        if (request.RealMoneyAmount <= 0)
        {
            throw new InvalidOperationException("Částka dobíjení musí být větší než 0.");
        }

        var bettor = await bettingRepository.GetOrCreateBettorAsync(request.BettorId, request.BettorName, cancellationToken);
        var wallet = await GetOrCreateWalletAsync(bettor, settings, cancellationToken);
        var currencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode) ? settings.BaseCurrencyCode : request.CurrencyCode.Trim().ToUpperInvariant();

        var payment = await paymentGateway.AuthorizeTopUpAsync(request.RealMoneyAmount, currencyCode, request.Reference, cancellationToken);
        if (!payment.Approved)
        {
            throw new InvalidOperationException("Testovací platební gateway dobíjení zamítla.");
        }

        var quote = await QuoteInternalAsync(Guid.Empty, request.RealMoneyAmount, 0m, cancellationToken);
        wallet.Balance += quote.CreditAmount;
        wallet.CreditCode = settings.CreditCode;
        wallet.LastMoneyToCreditRate = quote.MoneyToCreditRate;
        wallet.LastCreditToMoneyRate = quote.CreditToMoneyRate;
        wallet.UpdatedAtUtc = DateTime.UtcNow;

        dbContext.D3CreditTransactions.Add(new D3CreditTransaction
        {
            BettorId = bettor.Id,
            Type = D3CreditTransactionType.TopUp,
            CreditAmount = quote.CreditAmount,
            RealMoneyAmount = request.RealMoneyAmount,
            RealCurrencyCode = currencyCode,
            MoneyToCreditRate = quote.MoneyToCreditRate,
            CreditToMoneyRate = quote.CreditToMoneyRate,
            MarketParticipationMultiplier = quote.MarketParticipationMultiplier,
            Reference = payment.PaymentReference,
            Description = $"Dobití peněženky {settings.CreditCode} přes testovací gateway."
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return new D3CreditTopUpResponse(
            payment.GatewayName,
            payment.PaymentReference,
            settings.CreditCode,
            wallet.Balance,
            quote.CreditAmount,
            request.RealMoneyAmount,
            currencyCode,
            quote.MoneyToCreditRate);
    }

    public Task<D3CreditQuoteResponse> QuoteAsync(Guid marketId, D3CreditQuoteRequest request, CancellationToken cancellationToken)
    {
        return QuoteInternalAsync(marketId, request.RealMoneyAmount, request.CreditStake, cancellationToken);
    }

    public async Task<D3CreditBetPlacementResponse> PlaceCreditBetAsync(Guid marketId, D3CreditBetRequest request, CancellationToken cancellationToken)
    {
        var settings = await adminSettingsStore.LoadAsync(cancellationToken);
        if (request.CreditStake <= 0)
        {
            throw new InvalidOperationException("Sázka v D3Kreditu musí být větší než 0.");
        }

        var bettor = await bettingRepository.GetOrCreateBettorAsync(request.BettorId, request.BettorName, cancellationToken);
        var wallet = await GetOrCreateWalletAsync(bettor, settings, cancellationToken);
        var quote = await QuoteInternalAsync(marketId, 0m, request.CreditStake, cancellationToken);

        if (wallet.Balance < request.CreditStake)
        {
            throw new InvalidOperationException($"Peněženka nemá dostatek {settings.CreditCode} pro tuto sázku.");
        }

        var market = await bettingRepository.GetBettingMarketAsync(marketId, cancellationToken);
        if (!market.IsActive)
        {
            throw new InvalidOperationException("Na tuto událost momentálně nelze přijmout další sázky.");
        }

        var eventBettingLoad = await bettingRepository.GetMarketBettingLoadAsync(marketId, null, cancellationToken);
        var adjustedOdds = DynamicOddsCalculator.CalculateAdjustedOdds(
            market.OpeningOdds,
            eventBettingLoad.BetCount,
            eventBettingLoad.UniqueBettorCount,
            eventBettingLoad.TotalStake);

        wallet.Balance -= request.CreditStake;
        wallet.LastMoneyToCreditRate = quote.MoneyToCreditRate;
        wallet.LastCreditToMoneyRate = quote.CreditToMoneyRate;
        wallet.UpdatedAtUtc = DateTime.UtcNow;

        dbContext.Bets.Add(new Bet
        {
            BettingMarketId = market.Id,
            BettorId = bettor.Id,
            EventName = market.EventName,
            Odds = adjustedOdds,
            Stake = request.CreditStake,
            StakeCurrencyCode = settings.CreditCode,
            StakeRealMoneyEquivalent = quote.RealMoneyAmount,
            CreditToMoneyRateApplied = quote.CreditToMoneyRate,
            MarketParticipationMultiplierApplied = quote.MarketParticipationMultiplier,
            IsCommissionFeePaid = false,
            PlacedAtUtc = DateTime.UtcNow
        });

        dbContext.D3CreditTransactions.Add(new D3CreditTransaction
        {
            BettorId = bettor.Id,
            Type = D3CreditTransactionType.BetPlacement,
            CreditAmount = -request.CreditStake,
            RealMoneyAmount = -quote.RealMoneyAmount,
            RealCurrencyCode = quote.RealCurrencyCode,
            MoneyToCreditRate = quote.MoneyToCreditRate,
            CreditToMoneyRate = quote.CreditToMoneyRate,
            MarketParticipationMultiplier = quote.MarketParticipationMultiplier,
            Reference = market.Id.ToString(),
            Description = $"Vsazený {settings.CreditCode} na událost {market.EventName}."
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        await bettingRepository.RecalculateMarketCurrentOddsAsync(market.Id, cancellationToken);
        await bettingNotifier.NotifyBetCreatedAsync(cancellationToken);

        var walletResponse = await BuildWalletResponseAsync(bettor.Id, cancellationToken);
        return new D3CreditBetPlacementResponse(adjustedOdds, walletResponse, quote);
    }

    public async Task<D3CreditBetPlacementResponse> UpdateCreditBetAsync(Guid betId, Guid marketId, D3CreditBetRequest request, CancellationToken cancellationToken)
    {
        var settings = await adminSettingsStore.LoadAsync(cancellationToken);
        if (request.CreditStake <= 0)
        {
            throw new InvalidOperationException("Sázka v D3Kreditu musí být větší než 0.");
        }

        var bet = await dbContext.Bets.FirstOrDefaultAsync(x => x.Id == betId, cancellationToken)
            ?? throw new InvalidOperationException("Sázka nebyla nalezena.");

        var bettor = await bettingRepository.GetOrCreateBettorAsync(request.BettorId ?? bet.BettorId, request.BettorName, cancellationToken);
        var wallet = await GetOrCreateWalletAsync(bettor, settings, cancellationToken);
        var quote = await QuoteInternalAsync(marketId, 0m, request.CreditStake, cancellationToken);
        var market = await bettingRepository.GetBettingMarketAsync(marketId, cancellationToken);
        if (!market.IsActive)
        {
            throw new InvalidOperationException("Na tuto událost momentálně nelze přijmout další sázky.");
        }

        var eventBettingLoad = await bettingRepository.GetMarketBettingLoadAsync(marketId, betId, cancellationToken);
        var adjustedOdds = DynamicOddsCalculator.CalculateAdjustedOdds(
            market.OpeningOdds,
            eventBettingLoad.BetCount,
            eventBettingLoad.UniqueBettorCount,
            eventBettingLoad.TotalStake);

        var previousCreditStake = string.Equals(bet.StakeCurrencyCode, settings.CreditCode, StringComparison.OrdinalIgnoreCase)
            ? bet.Stake
            : 0m;

        var adjustedBalance = wallet.Balance + previousCreditStake - request.CreditStake;
        if (adjustedBalance < 0)
        {
            throw new InvalidOperationException($"Peněženka nemá dostatek {settings.CreditCode} pro tuto změnu sázky.");
        }

        wallet.Balance = adjustedBalance;
        wallet.LastMoneyToCreditRate = quote.MoneyToCreditRate;
        wallet.LastCreditToMoneyRate = quote.CreditToMoneyRate;
        wallet.UpdatedAtUtc = DateTime.UtcNow;

        bet.BettingMarketId = market.Id;
        bet.BettorId = bettor.Id;
        bet.EventName = market.EventName;
        bet.Odds = adjustedOdds;
        bet.Stake = request.CreditStake;
        bet.StakeCurrencyCode = settings.CreditCode;
        bet.StakeRealMoneyEquivalent = quote.RealMoneyAmount;
        bet.CreditToMoneyRateApplied = quote.CreditToMoneyRate;
        bet.MarketParticipationMultiplierApplied = quote.MarketParticipationMultiplier;
        bet.IsCommissionFeePaid = false;

        var deltaCredits = previousCreditStake - request.CreditStake;
        if (deltaCredits != 0m)
        {
            dbContext.D3CreditTransactions.Add(new D3CreditTransaction
            {
                BettorId = bettor.Id,
                Type = D3CreditTransactionType.ManualAdjustment,
                CreditAmount = deltaCredits,
                RealMoneyAmount = Math.Round(deltaCredits * quote.CreditToMoneyRate, 2, MidpointRounding.AwayFromZero),
                RealCurrencyCode = quote.RealCurrencyCode,
                MoneyToCreditRate = quote.MoneyToCreditRate,
                CreditToMoneyRate = quote.CreditToMoneyRate,
                MarketParticipationMultiplier = quote.MarketParticipationMultiplier,
                Reference = bet.Id.ToString(),
                Description = $"Úprava sázky v {settings.CreditCode} na událost {market.EventName}."
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await bettingRepository.RecalculateMarketCurrentOddsAsync(market.Id, cancellationToken);
        await bettingNotifier.NotifyBetCreatedAsync(cancellationToken);

        var walletResponse = await BuildWalletResponseAsync(bettor.Id, cancellationToken);
        return new D3CreditBetPlacementResponse(adjustedOdds, walletResponse, quote);
    }

    public async Task DeleteBetAsync(Guid betId, CancellationToken cancellationToken)
    {
        var settings = await adminSettingsStore.LoadAsync(cancellationToken);
        var bet = await dbContext.Bets.FirstOrDefaultAsync(x => x.Id == betId, cancellationToken)
            ?? throw new InvalidOperationException("Sázka nebyla nalezena.");

        if (string.Equals(bet.StakeCurrencyCode, settings.CreditCode, StringComparison.OrdinalIgnoreCase))
        {
            var wallet = await dbContext.BettorWallets.FirstOrDefaultAsync(x => x.BettorId == bet.BettorId, cancellationToken)
                ?? throw new InvalidOperationException("Peněženka sázejícího nebyla nalezena.");

            var refundCredits = bet.OutcomeStatus == BetOutcomeStatus.Won
                ? -bet.PotentialPayout
                : bet.Stake;

            wallet.Balance += refundCredits;
            wallet.UpdatedAtUtc = DateTime.UtcNow;

            dbContext.D3CreditTransactions.Add(new D3CreditTransaction
            {
                BettorId = bet.BettorId,
                Type = D3CreditTransactionType.ManualAdjustment,
                CreditAmount = refundCredits,
                RealMoneyAmount = Math.Round(refundCredits * bet.CreditToMoneyRateApplied, 2, MidpointRounding.AwayFromZero),
                RealCurrencyCode = settings.BaseCurrencyCode,
                MoneyToCreditRate = wallet.LastMoneyToCreditRate,
                CreditToMoneyRate = bet.CreditToMoneyRateApplied,
                MarketParticipationMultiplier = bet.MarketParticipationMultiplierApplied,
                Reference = bet.Id.ToString(),
                Description = bet.OutcomeStatus == BetOutcomeStatus.Won
                    ? "Reverze vyplacené výhry při smazání kreditové sázky."
                    : "Vrácení vkladu při smazání kreditové sázky."
            });
        }

        dbContext.Bets.Remove(bet);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (bet.BettingMarketId.HasValue)
        {
            await bettingRepository.RecalculateMarketCurrentOddsAsync(bet.BettingMarketId.Value, cancellationToken);
        }

        await bettingNotifier.NotifyBetCreatedAsync(cancellationToken);
    }

    public async Task SetBetOutcomeStatusAsync(Guid betId, BetOutcomeStatus outcomeStatus, CancellationToken cancellationToken)
    {
        var settings = await adminSettingsStore.LoadAsync(cancellationToken);
        var bet = await dbContext.Bets.FirstOrDefaultAsync(x => x.Id == betId, cancellationToken)
            ?? throw new InvalidOperationException("Sázka nebyla nalezena.");

        if (bet.OutcomeStatus == outcomeStatus)
        {
            return;
        }

        if (string.Equals(bet.StakeCurrencyCode, settings.CreditCode, StringComparison.OrdinalIgnoreCase))
        {
            var wallet = await dbContext.BettorWallets.FirstOrDefaultAsync(x => x.BettorId == bet.BettorId, cancellationToken)
                ?? throw new InvalidOperationException("Peněženka sázejícího nebyla nalezena.");

            decimal creditDelta = 0m;
            string? description = null;

            if (bet.OutcomeStatus == BetOutcomeStatus.Won && outcomeStatus != BetOutcomeStatus.Won)
            {
                creditDelta -= bet.PotentialPayout;
                description = "Reverze dříve vyplacené kreditové výhry.";
            }

            if (bet.OutcomeStatus != BetOutcomeStatus.Won && outcomeStatus == BetOutcomeStatus.Won)
            {
                creditDelta += bet.PotentialPayout;
                description = "Výplata kreditové výhry do peněženky sázejícího.";
            }

            if (creditDelta != 0m)
            {
                wallet.Balance += creditDelta;
                wallet.UpdatedAtUtc = DateTime.UtcNow;

                dbContext.D3CreditTransactions.Add(new D3CreditTransaction
                {
                    BettorId = bet.BettorId,
                    Type = D3CreditTransactionType.BetPayout,
                    CreditAmount = creditDelta,
                    RealMoneyAmount = Math.Round(creditDelta * bet.CreditToMoneyRateApplied, 2, MidpointRounding.AwayFromZero),
                    RealCurrencyCode = settings.BaseCurrencyCode,
                    MoneyToCreditRate = wallet.LastMoneyToCreditRate,
                    CreditToMoneyRate = bet.CreditToMoneyRateApplied,
                    MarketParticipationMultiplier = bet.MarketParticipationMultiplierApplied,
                    Reference = bet.Id.ToString(),
                    Description = description ?? "Změna vyhodnocení kreditové sázky."
                });
            }
        }

        bet.OutcomeStatus = outcomeStatus;
        bet.IsWinning = outcomeStatus == BetOutcomeStatus.Won;
        await dbContext.SaveChangesAsync(cancellationToken);
        await bettingNotifier.NotifyBetCreatedAsync(cancellationToken);
    }

    public Task<D3CreditAdminSettingsResponse> GetAdminSettingsAsync(CancellationToken cancellationToken)
    {
        return adminSettingsStore.LoadAsync(cancellationToken);
    }

    public Task<D3CreditAdminSettingsResponse> SaveAdminSettingsAsync(UpdateD3CreditAdminSettingsRequest request, CancellationToken cancellationToken)
    {
        return adminSettingsStore.SaveAsync(request, cancellationToken);
    }

    public async Task<IReadOnlyList<D3CreditAdminWalletListItemResponse>> GetAdminWalletsAsync(string? search, int limit, CancellationToken cancellationToken)
    {
        var settings = await adminSettingsStore.LoadAsync(cancellationToken);
        var query = dbContext.Bettors
            .AsNoTracking()
            .Include(x => x.Wallet)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.Name.Contains(term));
        }

        return await query
            .OrderBy(x => x.Name)
            .Take(Math.Clamp(limit, 1, 200))
            .Select(x => new D3CreditAdminWalletListItemResponse(
                x.Id,
                x.Name,
                x.Wallet != null ? x.Wallet.CreditCode : settings.CreditCode,
                x.Wallet != null ? x.Wallet.Balance : 0m,
                x.Wallet != null ? x.Wallet.LastMoneyToCreditRate : settings.BaseCreditsPerCurrencyUnit,
                x.Wallet != null ? x.Wallet.LastCreditToMoneyRate : settings.BaseCurrencyUnitsPerCredit,
                x.Wallet != null ? x.Wallet.UpdatedAtUtc : DateTime.MinValue))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<D3CreditAdminTransactionResponse>> GetAdminTransactionsAsync(string? search, int limit, CancellationToken cancellationToken)
    {
        var query = dbContext.D3CreditTransactions
            .AsNoTracking()
            .Include(x => x.Bettor)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                x.Bettor!.Name.Contains(term) ||
                x.Description.Contains(term) ||
                x.Reference.Contains(term));
        }

        return await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(Math.Clamp(limit, 1, 300))
            .Select(x => new D3CreditAdminTransactionResponse(
                x.Id,
                x.BettorId,
                x.Bettor!.Name,
                x.Type,
                x.CreditAmount,
                x.RealMoneyAmount,
                x.RealCurrencyCode,
                x.MoneyToCreditRate,
                x.CreditToMoneyRate,
                x.MarketParticipationMultiplier,
                x.Reference,
                x.Description,
                x.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<D3CreditWalletResponse> ApplyManualAdjustmentAsync(D3CreditManualAdjustmentRequest request, CancellationToken cancellationToken)
    {
        var settings = await adminSettingsStore.LoadAsync(cancellationToken);
        if (!settings.EnableManualCreditAdjustments)
        {
            throw new InvalidOperationException("Ruční kreditní úpravy jsou administrátorem vypnuté.");
        }

        if (request.CreditAmount == 0m)
        {
            throw new InvalidOperationException("Ruční úprava musí změnit kredit o nenulovou hodnotu.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new InvalidOperationException("Důvod ruční úpravy je povinný.");
        }

        var bettor = await bettingRepository.GetOrCreateBettorAsync(request.BettorId, request.BettorName, cancellationToken);
        var wallet = await GetOrCreateWalletAsync(bettor, settings, cancellationToken);

        if (wallet.Balance + request.CreditAmount < 0m)
        {
            throw new InvalidOperationException($"Peněženku nelze snížit pod 0 {settings.CreditCode}.");
        }

        wallet.Balance += request.CreditAmount;
        wallet.LastMoneyToCreditRate = settings.BaseCreditsPerCurrencyUnit;
        wallet.LastCreditToMoneyRate = settings.BaseCurrencyUnitsPerCredit;
        wallet.UpdatedAtUtc = DateTime.UtcNow;

        var currencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode)
            ? settings.BaseCurrencyCode
            : request.CurrencyCode.Trim().ToUpperInvariant();
        var realMoneyAmount = request.RealMoneyAmount
            ?? Math.Round(request.CreditAmount * settings.BaseCurrencyUnitsPerCredit, 2, MidpointRounding.AwayFromZero);

        dbContext.D3CreditTransactions.Add(new D3CreditTransaction
        {
            BettorId = bettor.Id,
            Type = D3CreditTransactionType.ManualAdjustment,
            CreditAmount = request.CreditAmount,
            RealMoneyAmount = realMoneyAmount,
            RealCurrencyCode = currencyCode,
            MoneyToCreditRate = settings.BaseCreditsPerCurrencyUnit,
            CreditToMoneyRate = settings.BaseCurrencyUnitsPerCredit,
            MarketParticipationMultiplier = 1m,
            Reference = string.IsNullOrWhiteSpace(request.Reference) ? $"MANUAL-{DateTime.UtcNow:yyyyMMddHHmmss}" : request.Reference.Trim(),
            Description = request.Reason.Trim()
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildWalletResponseAsync(bettor.Id, cancellationToken);
    }

    public async Task<D3CreditWalletResponse> RefundBetAsync(Guid betId, string reason, CancellationToken cancellationToken)
    {
        var settings = await adminSettingsStore.LoadAsync(cancellationToken);
        if (!settings.EnableManualBetRefunds)
        {
            throw new InvalidOperationException("Ruční refundy kreditních sázek jsou administrátorem vypnuté.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("Důvod refundu je povinný.");
        }

        var bet = await dbContext.Bets.FirstOrDefaultAsync(x => x.Id == betId, cancellationToken)
            ?? throw new InvalidOperationException("Sázka nebyla nalezena.");

        if (!string.Equals(bet.StakeCurrencyCode, settings.CreditCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Refund lze ručně vrátit jen u kreditové sázky.");
        }

        var wallet = await dbContext.BettorWallets.FirstOrDefaultAsync(x => x.BettorId == bet.BettorId, cancellationToken)
            ?? throw new InvalidOperationException("Peněženka sázejícího nebyla nalezena.");

        wallet.Balance += bet.Stake;
        wallet.UpdatedAtUtc = DateTime.UtcNow;

        dbContext.D3CreditTransactions.Add(new D3CreditTransaction
        {
            BettorId = bet.BettorId,
            Type = D3CreditTransactionType.Refund,
            CreditAmount = bet.Stake,
            RealMoneyAmount = bet.StakeRealMoneyEquivalent,
            RealCurrencyCode = settings.BaseCurrencyCode,
            MoneyToCreditRate = wallet.LastMoneyToCreditRate,
            CreditToMoneyRate = bet.CreditToMoneyRateApplied,
            MarketParticipationMultiplier = bet.MarketParticipationMultiplierApplied,
            Reference = bet.Id.ToString(),
            Description = reason.Trim()
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildWalletResponseAsync(bet.BettorId, cancellationToken);
    }

    private async Task<BettorWallet> GetOrCreateWalletAsync(Bettor bettor, D3CreditAdminSettingsResponse settings, CancellationToken cancellationToken)
    {
        var wallet = await dbContext.BettorWallets.FirstOrDefaultAsync(x => x.BettorId == bettor.Id, cancellationToken);
        if (wallet is not null)
        {
            return wallet;
        }

        wallet = new BettorWallet
        {
            BettorId = bettor.Id,
            CreditCode = settings.CreditCode,
            Balance = 0m,
            LastMoneyToCreditRate = settings.BaseCreditsPerCurrencyUnit,
            LastCreditToMoneyRate = settings.BaseCurrencyUnitsPerCredit
        };

        dbContext.BettorWallets.Add(wallet);
        await dbContext.SaveChangesAsync(cancellationToken);
        return wallet;
    }

    private async Task<D3CreditWalletResponse> BuildWalletResponseAsync(Guid bettorId, CancellationToken cancellationToken)
    {
        var bettor = await dbContext.Bettors
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == bettorId, cancellationToken)
            ?? throw new InvalidOperationException("Sázející nebyl nalezen.");

        var settings = await adminSettingsStore.LoadAsync(cancellationToken);
        var wallet = await dbContext.BettorWallets
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.BettorId == bettorId, cancellationToken)
            ?? new BettorWallet
            {
                BettorId = bettorId,
                CreditCode = settings.CreditCode,
                LastMoneyToCreditRate = settings.BaseCreditsPerCurrencyUnit,
                LastCreditToMoneyRate = settings.BaseCurrencyUnitsPerCredit
            };

        var transactions = await dbContext.D3CreditTransactions
            .AsNoTracking()
            .Where(x => x.BettorId == bettorId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(25)
            .ToListAsync(cancellationToken);

        return MapWallet(bettor, wallet, transactions);
    }

    private async Task<D3CreditQuoteResponse> QuoteInternalAsync(
        Guid marketId,
        decimal realMoneyAmount,
        decimal creditStake,
        CancellationToken cancellationToken)
    {
        var settings = await adminSettingsStore.LoadAsync(cancellationToken);
        BettingMarket? market = null;
        EventBettingLoadDto load = new(0, 0, 0m);
        D3CreditMarketAdminRuleResponse? marketRule = null;

        if (marketId != Guid.Empty)
        {
            market = await bettingRepository.GetBettingMarketAsync(marketId, cancellationToken);
            load = await bettingRepository.GetMarketBettingLoadAsync(marketId, null, cancellationToken);
            marketRule = settings.MarketRules.FirstOrDefault(x => x.MarketId == marketId && x.IsEnabled);
        }

        var participationMultiplier = CalculateParticipationMultiplier(settings, marketRule, load.UniqueBettorCount, load.TotalStake, market?.CurrentOdds ?? 1m);
        var moneyToCreditRate = marketRule?.OverrideMoneyToCreditRate is > 0m
            ? marketRule.OverrideMoneyToCreditRate.Value
            : Math.Round(settings.BaseCreditsPerCurrencyUnit * participationMultiplier, 4, MidpointRounding.AwayFromZero);
        var creditToMoneyRate = marketRule?.OverrideCreditToMoneyRate is > 0m
            ? marketRule.OverrideCreditToMoneyRate.Value
            : Math.Round(settings.BaseCurrencyUnitsPerCredit / Math.Max(participationMultiplier, 0.01m), 4, MidpointRounding.AwayFromZero);

        var effectiveRealMoney = realMoneyAmount > 0
            ? realMoneyAmount
            : Math.Round(creditStake * creditToMoneyRate, 2, MidpointRounding.AwayFromZero);

        var effectiveCreditAmount = creditStake > 0
            ? creditStake
            : Math.Round(realMoneyAmount * moneyToCreditRate, 2, MidpointRounding.AwayFromZero);

        var potentialPayoutCredits = market is null
            ? 0m
            : Math.Round(effectiveCreditAmount * market.CurrentOdds, 2, MidpointRounding.AwayFromZero);

        var potentialPayoutRealMoney = Math.Round(potentialPayoutCredits * creditToMoneyRate, 2, MidpointRounding.AwayFromZero);

        return new D3CreditQuoteResponse(
            marketId,
            market?.EventName ?? "Dobití D3Kreditu",
            settings.CreditCode,
            settings.BaseCurrencyCode,
            moneyToCreditRate,
            creditToMoneyRate,
            participationMultiplier,
            effectiveRealMoney,
            effectiveCreditAmount,
            potentialPayoutCredits,
            potentialPayoutRealMoney);
    }

    private static decimal CalculateParticipationMultiplier(
        D3CreditAdminSettingsResponse settings,
        D3CreditMarketAdminRuleResponse? marketRule,
        int uniqueBettorCount,
        decimal totalStake,
        decimal odds)
    {
        var multiplier = 1m;

        if (uniqueBettorCount <= settings.LowParticipationThreshold)
        {
            multiplier += settings.LowParticipationBoostPercent / 100m;
        }
        else if (uniqueBettorCount >= settings.HighParticipationThreshold)
        {
            multiplier -= settings.HighParticipationReductionPercent / 100m;
        }

        if (settings.TotalStakePressureDivisor > 0)
        {
            var pressureRatio = Math.Min(1m, totalStake / settings.TotalStakePressureDivisor);
            multiplier -= pressureRatio * (settings.MaxPressureReductionPercent / 100m);
        }

        if (odds > 1m)
        {
            multiplier += Math.Min(0.25m, (odds - 1m) * (settings.OddsVolatilityWeightPercent / 100m));
        }

        if (marketRule is not null)
        {
            multiplier *= 1m + (marketRule.AdditionalMultiplierPercent / 100m);
        }

        return Math.Max(0.25m, Math.Round(multiplier, 4, MidpointRounding.AwayFromZero));
    }

    private static D3CreditWalletResponse MapWallet(Bettor bettor, BettorWallet wallet, IReadOnlyCollection<D3CreditTransaction> transactions)
    {
        return new D3CreditWalletResponse(
            bettor.Id,
            bettor.Name,
            wallet.CreditCode,
            wallet.Balance,
            wallet.LastMoneyToCreditRate,
            wallet.LastCreditToMoneyRate,
            transactions
                .OrderByDescending(x => x.CreatedAtUtc)
                .Select(x => new D3CreditTransactionResponse(
                    x.Id,
                    x.Type,
                    x.CreditAmount,
                    x.RealMoneyAmount,
                    x.RealCurrencyCode,
                    x.MoneyToCreditRate,
                    x.CreditToMoneyRate,
                    x.MarketParticipationMultiplier,
                    x.Reference,
                    x.Description,
                    x.CreatedAtUtc))
                .ToArray());
    }
}
