namespace BettingApp.Server.Configuration;

public sealed class OperatorOAuthClientOptions
{
    public const string SectionName = "OAuth:OperatorClient";

    public string ClientId { get; set; } = "d3bet-wpf";

    public string DisplayName { get; set; } = "D3Bet Operator Client";

    public string RedirectUri { get; set; } = "http://127.0.0.1:43123/callback/";
}
