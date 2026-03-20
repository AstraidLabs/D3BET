using BettingApp.Application.Abstractions;
using BettingApp.Application.Services;
using BettingApp.Domain.Entities;
using MediatR;

namespace BettingApp.Application.Bets.Commands;

public sealed class CreateBetCommandHandler(
    IBettingRepository repository,
    IBettingNotifier notifier) : IRequestHandler<CreateBetCommand, decimal>
{
    public async Task<decimal> Handle(CreateBetCommand request, CancellationToken cancellationToken)
    {
        Validate(request);

        var market = await repository.GetBettingMarketAsync(request.MarketId, cancellationToken);
        if (!market.IsActive)
        {
            throw new InvalidOperationException("Na tuto událost momentálně nelze přijmout další sázky.");
        }

        var eventBettingLoad = await repository.GetMarketBettingLoadAsync(request.MarketId, null, cancellationToken);
        var adjustedOdds = DynamicOddsCalculator.CalculateAdjustedOdds(
            market.OpeningOdds,
            eventBettingLoad.BetCount,
            eventBettingLoad.UniqueBettorCount,
            eventBettingLoad.TotalStake);

        var bettor = await repository.GetOrCreateBettorAsync(
            request.BettorId,
            request.BettorName,
            cancellationToken);

        var bet = new Bet
        {
            BettingMarketId = market.Id,
            BettorId = bettor.Id,
            EventName = market.EventName,
            Odds = adjustedOdds,
            Stake = request.Stake,
            IsCommissionFeePaid = request.IsCommissionFeePaid,
            PlacedAtUtc = DateTime.UtcNow
        };

        await repository.AddBetAsync(bet, cancellationToken);
        await repository.RecalculateMarketCurrentOddsAsync(market.Id, cancellationToken);
        await notifier.NotifyBetCreatedAsync(cancellationToken);
        return adjustedOdds;
    }

    private static void Validate(CreateBetCommand request)
    {
        if (request.MarketId == Guid.Empty)
        {
            throw new InvalidOperationException("Vyberte platnou vypsanou událost.");
        }

        if (request.Stake <= 0)
        {
            throw new InvalidOperationException("Castka sazky musi byt vetsi nez 0.");
        }

        var hasExistingBettor = request.BettorId.HasValue && request.BettorId.Value != Guid.Empty;
        var hasNewBettorName = !string.IsNullOrWhiteSpace(request.BettorName);

        if (!hasExistingBettor && !hasNewBettorName)
        {
            throw new InvalidOperationException("Vyberte sazejiciho nebo zadejte nove jmeno.");
        }
    }
}
