using Microsoft.AspNetCore.Identity;
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

        var scopes = requestedScopes
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var identity = new ClaimsIdentity(
            OpenIddict.Server.AspNetCore.OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            OpenIddictConstants.Claims.Name,
            OpenIddictConstants.Claims.Role);

        identity.SetClaim(OpenIddictConstants.Claims.Subject, user.Id);
        identity.SetClaim(OpenIddictConstants.Claims.Name, user.UserName ?? user.Id);

        foreach (var role in await userManager.GetRolesAsync(user))
        {
            identity.AddClaim(new Claim(OpenIddictConstants.Claims.Role, role));
        }

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(scopes);
        principal.SetResources("d3bet-api");

        return principal;
    }
}
