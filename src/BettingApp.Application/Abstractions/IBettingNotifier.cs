namespace BettingApp.Application.Abstractions;

public interface IBettingNotifier
{
    Task NotifyBetCreatedAsync(CancellationToken cancellationToken);
}
