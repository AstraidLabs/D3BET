namespace BettingApp.Server.Services;

public interface IGatewayOAuth2TokenClient
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}
