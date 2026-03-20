using BettingApp.Application.Abstractions;
using MediatR;

namespace BettingApp.Application.Bets.Commands;

public sealed class UpdateBettingMarketCommandHandler(
    IBettingRepository repository) : IRequestHandler<UpdateBettingMarketCommand>
{
    public async Task Handle(UpdateBettingMarketCommand request, CancellationToken cancellationToken)
    {
        Validate(request);

        await repository.UpdateBettingMarketAsync(
            request.MarketId,
            request.EventName.Trim(),
            request.OpeningOdds,
            request.IsActive,
            cancellationToken);
    }

    private static void Validate(UpdateBettingMarketCommand request)
    {
        if (request.MarketId == Guid.Empty)
        {
            throw new InvalidOperationException("Událost nemá platné ID.");
        }

        if (string.IsNullOrWhiteSpace(request.EventName))
        {
            throw new InvalidOperationException("Vyplňte název události.");
        }

        if (request.OpeningOdds <= 1m)
        {
            throw new InvalidOperationException("Výchozí kurz musí být větší než 1.");
        }
    }
}
