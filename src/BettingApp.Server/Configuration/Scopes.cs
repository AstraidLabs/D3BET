namespace BettingApp.Server.Configuration;

public static class Scopes
{
    public const string DisplayRead = "display.read";
    public const string Operations = "operations";
    public const string OpenId = OpenIddict.Abstractions.OpenIddictConstants.Scopes.OpenId;
    public const string Profile = OpenIddict.Abstractions.OpenIddictConstants.Scopes.Profile;
    public const string OfflineAccess = OpenIddict.Abstractions.OpenIddictConstants.Scopes.OfflineAccess;
    public const string Roles = OpenIddict.Abstractions.OpenIddictConstants.Scopes.Roles;
}
