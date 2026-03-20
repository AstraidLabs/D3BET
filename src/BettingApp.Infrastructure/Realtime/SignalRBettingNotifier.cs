using BettingApp.Application.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace BettingApp.Infrastructure.Realtime;

public sealed class SignalRBettingNotifier(IHubContext<BetsHub> hubContext) : IBettingNotifier
{
    public Task NotifyBetCreatedAsync(CancellationToken cancellationToken)
    {
        return hubContext.Clients.All.SendAsync(BetsHub.BetCreatedMethod, cancellationToken);
    }
}
