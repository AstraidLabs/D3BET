namespace BettingApp.Server.Configuration;

public sealed class AccountEmailOptions
{
    public const string SectionName = "AccountEmail";

    public string Mode { get; set; } = "Preview";

    public string FromAddress { get; set; } = "noreply@d3bet.local";

    public string FromName { get; set; } = "D3Bet";

    public string ActivationBaseUrl { get; set; } = "d3bet://account/activate";

    public string ResetPasswordBaseUrl { get; set; } = "d3bet://account/reset-password";

    public SmtpOptions Smtp { get; set; } = new();

    public GatewayOAuth2Options GatewayOAuth2 { get; set; } = new();
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 25;

    public bool UseStartTls { get; set; }

    public bool UseSsl { get; set; }

    public bool RequireAuthentication { get; set; }

    public string UserName { get; set; } = string.Empty;
}

public sealed class GatewayOAuth2Options
{
    public string TokenUrl { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string Scope { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;
}
