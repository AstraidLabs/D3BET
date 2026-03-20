namespace BettingApp.Server.Configuration;

public sealed class ServerDiscoveryOptions
{
    public const string SectionName = "Discovery";

    public int UdpPort { get; init; } = 55103;

    public string RequestToken { get; init; } = "D3BET_DISCOVERY_V1";

    public string ServerName { get; init; } = "D3Bet Server";
}
