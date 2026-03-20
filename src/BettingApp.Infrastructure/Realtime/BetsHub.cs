using Microsoft.AspNetCore.SignalR;

namespace BettingApp.Infrastructure.Realtime;

public sealed class BetsHub : Hub
{
    public const string HubRoute = "/hubs/bets";
    public const string BetCreatedMethod = "BetCreated";
}
