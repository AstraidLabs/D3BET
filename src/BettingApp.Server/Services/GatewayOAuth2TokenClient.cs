using System.Net.Http.Json;
using System.Text.Json;
using BettingApp.Server.Configuration;
using Microsoft.Extensions.Options;

namespace BettingApp.Server.Services;

public sealed class GatewayOAuth2TokenClient(
    HttpClient httpClient,
    IOptions<AccountEmailOptions> options,
    ILogger<GatewayOAuth2TokenClient> logger) : IGatewayOAuth2TokenClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly GatewayOAuth2Options gatewayOptions = options.Value.GatewayOAuth2;
    private readonly SemaphoreSlim tokenSemaphore = new(1, 1);
    private string? cachedAccessToken;
    private DateTimeOffset expiresAtUtc = DateTimeOffset.MinValue;

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(cachedAccessToken) && expiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return cachedAccessToken;
        }

        await tokenSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (!string.IsNullOrWhiteSpace(cachedAccessToken) && expiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(1))
            {
                return cachedAccessToken;
            }

            ValidateConfiguration();

            var payload = new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = gatewayOptions.ClientId,
                ["client_secret"] = gatewayOptions.ClientSecret
            };

            if (!string.IsNullOrWhiteSpace(gatewayOptions.Scope))
            {
                payload["scope"] = gatewayOptions.Scope;
            }

            if (!string.IsNullOrWhiteSpace(gatewayOptions.Audience))
            {
                payload["audience"] = gatewayOptions.Audience;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, gatewayOptions.TokenUrl)
            {
                Content = new FormUrlEncodedContent(payload)
            };

            using var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var tokenResponse = await JsonSerializer.DeserializeAsync<TokenResponse>(stream, JsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("OAuth2 gateway vrátila prázdnou odpověď.");

            if (string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
            {
                throw new InvalidOperationException("OAuth2 gateway nevrátila access token.");
            }

            cachedAccessToken = tokenResponse.AccessToken;
            expiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, tokenResponse.ExpiresIn));

            logger.LogInformation("OAuth2 token pro e-mailovou gateway byl úspěšně obnoven.");
            return cachedAccessToken;
        }
        finally
        {
            tokenSemaphore.Release();
        }
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(gatewayOptions.TokenUrl))
        {
            throw new InvalidOperationException("Chybí konfigurace AccountEmail:GatewayOAuth2:TokenUrl.");
        }

        if (string.IsNullOrWhiteSpace(gatewayOptions.ClientId))
        {
            throw new InvalidOperationException("Chybí konfigurace AccountEmail:GatewayOAuth2:ClientId.");
        }

        if (string.IsNullOrWhiteSpace(gatewayOptions.ClientSecret))
        {
            throw new InvalidOperationException("Chybí konfigurace AccountEmail:GatewayOAuth2:ClientSecret.");
        }
    }

    private sealed class TokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;

        public int ExpiresIn { get; set; } = 3600;
    }
}
