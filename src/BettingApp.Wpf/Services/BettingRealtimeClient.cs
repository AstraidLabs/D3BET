using BettingApp.Infrastructure.Realtime;
using Microsoft.AspNetCore.SignalR.Client;

namespace BettingApp.Wpf.Services;

public sealed class BettingRealtimeClient : IAsyncDisposable
{
    private readonly HubConnection hubConnection;

    public event Func<Task>? BetCreated;

    public BettingRealtimeClient(OperatorAuthOptions authOptions, OperatorAuthService operatorAuthService)
    {
        hubConnection = new HubConnectionBuilder()
            .WithUrl($"{authOptions.ServerBaseUrl.TrimEnd('/')}{BetsHub.HubRoute}", options =>
            {
                options.AccessTokenProvider = async () => await operatorAuthService.GetAccessTokenAsync(CancellationToken.None);
            })
            .WithAutomaticReconnect()
            .Build();

        hubConnection.On(BetsHub.BetCreatedMethod, async () =>
        {
            if (BetCreated is not null)
            {
                await BetCreated.Invoke();
            }
        });
    }

    public async Task StartAsync()
    {
        if (hubConnection.State == HubConnectionState.Disconnected)
        {
            await hubConnection.StartAsync();
        }
    }

    public async Task StopAsync()
    {
        if (hubConnection.State != HubConnectionState.Disconnected)
        {
            await hubConnection.StopAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await hubConnection.DisposeAsync();
    }
}
