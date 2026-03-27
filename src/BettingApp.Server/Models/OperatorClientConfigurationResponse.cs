namespace BettingApp.Server.Models;

public sealed record OperatorClientConfigurationResponse(
    string ClientId,
    string RedirectUri,
    string Scope,
    string DisplayName);
