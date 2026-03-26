using Microsoft.AspNetCore.Identity;
using BettingApp.Server.Configuration;
using OpenIddict.Abstractions;
using System.Security.Claims;

namespace BettingApp.Server.Services;

public sealed class OperatorPrincipalFactory(UserManager<IdentityUser> userManager)
{
    public async Task<ClaimsPrincipal> CreateAsync(
        IdentityUser user,
        IEnumerable<string> requestedScopes,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var roles = await userManager.GetRolesAsync(user);
        var scopes = requestedScopes
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Where(scope =>
            scope is Configuration.Scopes.OpenId or Configuration.Scopes.Profile or Configuration.Scopes.OfflineAccess or Configuration.Scopes.Roles ||
                (scope == Configuration.Scopes.Operations && roles.Any(role =>
                    string.Equals(role, Roles.Admin, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(role, Roles.Operator, StringComparison.OrdinalIgnoreCase))) ||
                (scope == Configuration.Scopes.DisplayRead && roles.Any(role =>
                    string.Equals(role, Roles.Admin, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(role, Roles.Operator, StringComparison.OrdinalIgnoreCase))))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var identity = new ClaimsIdentity(
            OpenIddict.Server.AspNetCore.OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            OpenIddictConstants.Claims.Name,
            OpenIddictConstants.Claims.Role);

        identity.SetClaim(OpenIddictConstants.Claims.Subject, user.Id);
        identity.SetClaim(OpenIddictConstants.Claims.Name, user.UserName ?? user.Id);

        foreach (var role in roles)
        {
            identity.AddClaim(new Claim(OpenIddictConstants.Claims.Role, role));
        }

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(scopes);
        principal.SetResources("d3bet-api");
        principal.SetDestinations(static claim => claim.Type switch
        {
            OpenIddictConstants.Claims.Subject => [OpenIddictConstants.Destinations.AccessToken],
            OpenIddictConstants.Claims.Name => [OpenIddictConstants.Destinations.AccessToken],
            OpenIddictConstants.Claims.Role => [OpenIddictConstants.Destinations.AccessToken],
            _ => []
        });

        return principal;
    }
}
