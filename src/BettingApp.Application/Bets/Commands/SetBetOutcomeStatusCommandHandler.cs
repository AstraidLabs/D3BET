using BettingApp.Application.Abstractions;
using MediatR;

namespace BettingApp.Application.Bets.Commands;

public sealed class SetBetOutcomeStatusCommandHandler(
    IBettingRepository repository,
    IBettingNotifier notifier) : IRequestHandler<SetBetOutcomeStatusCommand>
{
    public async Task Handle(SetBetOutcomeStatusCommand request, CancellationToken cancellationToken)
    {
        if (request.BetId == Guid.Empty)
        {
            throw new InvalidOperationException("Sázka nemá platné ID.");
        }

        await repository.SetBetOutcomeStatusAsync(request.BetId, request.OutcomeStatus, cancellationToken);
        await notifier.NotifyBetCreatedAsync(cancellationToken);
    }
}
