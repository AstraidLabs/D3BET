namespace BettingApp.Wpf.Services;

public sealed class ServerConnectionContext
{
    public string ServerName { get; private set; } = "D3Bet Server";

    public string MachineName { get; private set; } = "Neznámý uzel";

    public string BaseUrl { get; private set; } = "http://localhost:5103";

    public string DiscoverySource { get; private set; } = "Výchozí adresa";

    public void Update(string serverName, string machineName, string baseUrl, string discoverySource)
    {
        ServerName = string.IsNullOrWhiteSpace(serverName) ? "D3Bet Server" : serverName;
        MachineName = string.IsNullOrWhiteSpace(machineName) ? "Neznámý uzel" : machineName;
        BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? BaseUrl : baseUrl;
        DiscoverySource = string.IsNullOrWhiteSpace(discoverySource) ? "Výchozí adresa" : discoverySource;
    }
}
