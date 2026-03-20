using BettingApp.Application.Abstractions;
using BettingApp.Domain.Entities;
using MediatR;

namespace BettingApp.Application.Bets.Commands;

public sealed class CreateBettingMarketCommandHandler(
    IBettingRepository repository) : IRequestHandler<CreateBettingMarketCommand, Guid>
{
    public async Task<Guid> Handle(CreateBettingMarketCommand request, CancellationToken cancellationToken)
    {
        Validate(request);

        var market = new BettingMarket
        {
            EventName = request.EventName.Trim(),
            OpeningOdds = request.OpeningOdds,
            CurrentOdds = request.OpeningOdds,
            IsActive = request.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };

        await repository.AddBettingMarketAsync(market, cancellationToken);
        return market.Id;
    }

    private static void Validate(CreateBettingMarketCommand request)
    {
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
