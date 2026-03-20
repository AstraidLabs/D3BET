using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace BettingApp.Wpf.Services;

public sealed class ServerDiscoveryService(
    OperatorAuthOptions authOptions,
    ServerConnectionContext serverConnectionContext)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(2)
    };

    public async Task<string> DiscoverAndApplyAsync(CancellationToken cancellationToken = default)
    {
        if (await IsHealthyAsync(authOptions.ServerBaseUrl, cancellationToken))
        {
            serverConnectionContext.Update("D3Bet Server", Environment.MachineName, authOptions.ServerBaseUrl, "Výchozí adresa");
            return authOptions.ServerBaseUrl;
        }

        var discoveredServer = await TryDiscoverViaUdpAsync(cancellationToken)
            ?? await TryProbeFallbackUrlsAsync(cancellationToken);

        if (discoveredServer is null || string.IsNullOrWhiteSpace(discoveredServer.BaseUrl))
        {
            throw new InvalidOperationException("Server D3Bet se nepodařilo najít v lokální síti ani na očekávaných adresách.");
        }

        authOptions.ServerBaseUrl = discoveredServer.BaseUrl;
        serverConnectionContext.Update(
            discoveredServer.ServerName,
            discoveredServer.MachineName,
            discoveredServer.BaseUrl,
            discoveredServer.Source);
        return discoveredServer.BaseUrl;
    }

    private async Task<DiscoveredServer?> TryDiscoverViaUdpAsync(CancellationToken cancellationToken)
    {
        using var udpClient = new UdpClient(AddressFamily.InterNetwork)
        {
            EnableBroadcast = true
        };

        var payload = Encoding.UTF8.GetBytes(authOptions.DiscoveryRequestToken);
        await udpClient.SendAsync(payload, new IPEndPoint(IPAddress.Broadcast, authOptions.DiscoveryUdpPort), cancellationToken);

        var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(2));

        while (!timeoutCts.IsCancellationRequested)
        {
            UdpReceiveResult received;
            try
            {
                received = await udpClient.ReceiveAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            var response = JsonSerializer.Deserialize<DiscoveryResponse>(received.Buffer, SerializerOptions);
            if (response?.BaseUrls is null || response.BaseUrls.Length == 0)
            {
                continue;
            }

            foreach (var baseUrl in response.BaseUrls)
            {
                if (await IsHealthyAsync(baseUrl, cancellationToken))
                {
                    return new DiscoveredServer(
                        response.ServerName,
                        response.MachineName,
                        baseUrl,
                        "Automatické nalezení v síti");
                }
            }
        }

        return null;
    }

    private async Task<DiscoveredServer?> TryProbeFallbackUrlsAsync(CancellationToken cancellationToken)
    {
        foreach (var baseUrl in authOptions.FallbackServerBaseUrls)
        {
            if (await IsHealthyAsync(baseUrl, cancellationToken))
            {
                return new DiscoveredServer("D3Bet Server", Environment.MachineName, baseUrl, "Nouzová fallback adresa");
            }
        }

        return null;
    }

    private async Task<bool> IsHealthyAsync(string? baseUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return false;
        }

        try
        {
            var normalizedBaseUrl = baseUrl.TrimEnd('/');
            var response = await httpClient.GetAsync($"{normalizedBaseUrl}/api/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private sealed class DiscoveryResponse
    {
        public string ServerName { get; set; } = string.Empty;

        public string MachineName { get; set; } = string.Empty;

        public int UdpPort { get; set; }

        public string[] BaseUrls { get; set; } = [];
    }

    private sealed record DiscoveredServer(string ServerName, string MachineName, string BaseUrl, string Source);
}
