using BettingApp.Application.Abstractions;
using MediatR;

namespace BettingApp.Application.Bets.Commands;

public sealed class DeleteBetCommandHandler(
    IBettingRepository repository,
    IBettingNotifier notifier) : IRequestHandler<DeleteBetCommand>
{
    public async Task Handle(DeleteBetCommand request, CancellationToken cancellationToken)
    {
        if (request.BetId == Guid.Empty)
        {
            throw new InvalidOperationException("Sazka nema platne ID.");
        }

        await repository.DeleteBetAsync(request.BetId, cancellationToken);
        await notifier.NotifyBetCreatedAsync(cancellationToken);
    }
}
