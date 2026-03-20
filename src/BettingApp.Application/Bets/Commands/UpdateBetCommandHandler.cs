using BettingApp.Application.Abstractions;
using BettingApp.Application.Services;
using MediatR;

namespace BettingApp.Application.Bets.Commands;

public sealed class UpdateBetCommandHandler(
    IBettingRepository repository,
    IBettingNotifier notifier) : IRequestHandler<UpdateBetCommand, decimal>
{
    public async Task<decimal> Handle(UpdateBetCommand request, CancellationToken cancellationToken)
    {
        Validate(request);

        var existingBet = await repository.GetBetAsync(request.BetId, cancellationToken);
        var market = await repository.GetBettingMarketAsync(request.MarketId, cancellationToken);
        if (!market.IsActive)
        {
            throw new InvalidOperationException("Na tuto událost momentálně nelze přijmout další sázky.");
        }

        var eventBettingLoad = await repository.GetMarketBettingLoadAsync(
            request.MarketId,
            request.BetId,
            cancellationToken);
        var adjustedOdds = DynamicOddsCalculator.CalculateAdjustedOdds(
            market.OpeningOdds,
            eventBettingLoad.BetCount,
            eventBettingLoad.UniqueBettorCount,
            eventBettingLoad.TotalStake);

        var bettor = await repository.GetOrCreateBettorAsync(
            request.BettorId,
            request.BettorName,
            cancellationToken);

        await repository.UpdateBetAsync(
            request.BetId,
            market.Id,
            bettor.Id,
            market.EventName,
            adjustedOdds,
            request.Stake,
            request.IsCommissionFeePaid,
            cancellationToken);

        if (existingBet.BettingMarketId.HasValue && existingBet.BettingMarketId.Value != market.Id)
        {
            await repository.RecalculateMarketCurrentOddsAsync(existingBet.BettingMarketId.Value, cancellationToken);
        }

        await repository.RecalculateMarketCurrentOddsAsync(market.Id, cancellationToken);

        await notifier.NotifyBetCreatedAsync(cancellationToken);
        return adjustedOdds;
    }

    private static void Validate(UpdateBetCommand request)
    {
        if (request.BetId == Guid.Empty)
        {
            throw new InvalidOperationException("Sazka nema platne ID.");
        }

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
