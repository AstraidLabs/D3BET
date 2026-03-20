namespace BettingApp.Server.Configuration;

public sealed class KioskOAuthClientOptions
{
    public const string SectionName = "OAuth:KioskClient";

    public string ClientId { get; set; } = "d3bet-kiosk";

    public string ClientSecret { get; set; } = "D3Bet-Kiosk-Secret-ChangeMe";

    public string DisplayName { get; set; } = "D3Bet Customer Display";
}
