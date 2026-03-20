using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using BettingApp.Server.Configuration;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Options;

namespace BettingApp.Server.Services;

public sealed class ServerDiscoveryHostedService(
    IServer server,
    IHostApplicationLifetime applicationLifetime,
    IOptions<ServerDiscoveryOptions> options,
    ILogger<ServerDiscoveryHostedService> logger) : BackgroundService
{
    private readonly ServerDiscoveryOptions discoveryOptions = options.Value;
    private UdpClient? udpClient;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await WaitForApplicationStartedAsync(stoppingToken);

        udpClient = new UdpClient(AddressFamily.InterNetwork)
        {
            EnableBroadcast = true
        };
        udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, discoveryOptions.UdpPort));

        logger.LogInformation("UDP discovery naslouchá na portu {Port}.", discoveryOptions.UdpPort);

        while (!stoppingToken.IsCancellationRequested)
        {
            UdpReceiveResult received;
            try
            {
                received = await udpClient.ReceiveAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Discovery listener narazil na chybu při příjmu UDP zprávy.");
                continue;
            }

            var payload = Encoding.UTF8.GetString(received.Buffer);
            if (!string.Equals(payload, discoveryOptions.RequestToken, StringComparison.Ordinal))
            {
                continue;
            }

            var response = CreateResponse(received.RemoteEndPoint);
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response));

            try
            {
                await udpClient.SendAsync(bytes, received.RemoteEndPoint, stoppingToken);
                logger.LogInformation(
                    "Discovery odpověď byla odeslána klientovi {RemoteAddress}:{RemotePort}.",
                    received.RemoteEndPoint.Address,
                    received.RemoteEndPoint.Port);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Discovery odpověď se nepodařilo odeslat klientovi.");
            }
        }
    }

    public override void Dispose()
    {
        udpClient?.Dispose();
        base.Dispose();
    }

    private async Task WaitForApplicationStartedAsync(CancellationToken cancellationToken)
    {
        if (applicationLifetime.ApplicationStarted.IsCancellationRequested)
        {
            return;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = applicationLifetime.ApplicationStarted.Register(() => completion.TrySetResult());
        await completion.Task.WaitAsync(cancellationToken);
    }

    private DiscoveryResponse CreateResponse(IPEndPoint remoteEndPoint)
    {
        var urls = BuildAdvertisedUrls(remoteEndPoint).ToArray();
        return new DiscoveryResponse
        {
            ServerName = discoveryOptions.ServerName,
            MachineName = Environment.MachineName,
            UdpPort = discoveryOptions.UdpPort,
            BaseUrls = urls
        };
    }

    private IEnumerable<string> BuildAdvertisedUrls(IPEndPoint remoteEndPoint)
    {
        var addressesFeature = server.Features.Get<IServerAddressesFeature>();
        var configuredUrls = addressesFeature?.Addresses ?? [];

        var distinct = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var address in configuredUrls)
        {
            if (!Uri.TryCreate(address, UriKind.Absolute, out var uri))
            {
                continue;
            }

            if (IsWildcardHost(uri.Host) || IsLoopbackHost(uri.Host))
            {
                foreach (var replacementHost in GetCandidateHosts(remoteEndPoint.Address))
                {
                    distinct.Add($"{uri.Scheme}://{replacementHost}:{uri.Port}");
                }

                continue;
            }

            distinct.Add($"{uri.Scheme}://{uri.Host}:{uri.Port}");
        }

        if (distinct.Count == 0)
        {
            foreach (var replacementHost in GetCandidateHosts(remoteEndPoint.Address))
            {
                distinct.Add($"http://{replacementHost}:5103");
            }
        }

        return distinct;
    }

    private static IEnumerable<string> GetCandidateHosts(IPAddress remoteAddress)
    {
        yield return Environment.MachineName;

        var localIpv4Addresses = NetworkInterface.GetAllNetworkInterfaces()
            .Where(network => network.OperationalStatus == OperationalStatus.Up)
            .SelectMany(network => network.GetIPProperties().UnicastAddresses)
            .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address.Address))
            .Select(address => address.Address.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!IPAddress.IsLoopback(remoteAddress))
        {
            localIpv4Addresses.Insert(0, remoteAddress.AddressFamily == AddressFamily.InterNetwork ? GetBestLocalAddressForRemote(remoteAddress) : string.Empty);
        }

        foreach (var address in localIpv4Addresses.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            yield return address;
        }

        yield return "localhost";
    }

    private static string GetBestLocalAddressForRemote(IPAddress remoteAddress)
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(remoteAddress, 9);
            return ((IPEndPoint)socket.LocalEndPoint!).Address.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsWildcardHost(string host) =>
        string.Equals(host, "0.0.0.0", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(host, "[::]", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(host, "*", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(host, "+", StringComparison.OrdinalIgnoreCase);

    private static bool IsLoopbackHost(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase);

    private sealed class DiscoveryResponse
    {
        public string ServerName { get; set; } = string.Empty;

        public string MachineName { get; set; } = string.Empty;

        public int UdpPort { get; set; }

        public string[] BaseUrls { get; set; } = [];
    }
}
