namespace BettingApp.Wpf.Services;

public sealed class OperatorAuthOptions
{
    public string ServerBaseUrl { get; set; } = "http://localhost:5103";

    public int DiscoveryUdpPort { get; init; } = 55103;

    public string DiscoveryRequestToken { get; init; } = "D3BET_DISCOVERY_V1";

    public string[] FallbackServerBaseUrls { get; init; } =
    [
        "http://localhost:5103",
        "http://127.0.0.1:5103"
    ];

    public string ClientId { get; init; } = "d3bet-wpf-dev";

    public string RedirectUri { get; init; } = "http://127.0.0.1:43123/callback/";

    public string Scope { get; init; } = "openid profile roles offline_access operations";
}
