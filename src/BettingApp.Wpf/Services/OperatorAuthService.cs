using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BettingApp.Wpf.Services;

public sealed class OperatorAuthService(
    OperatorAuthOptions options,
    OperatorSessionStore sessionStore,
    OperatorSessionContext sessionContext)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim sessionSemaphore = new(1, 1);
    private readonly HttpClient httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public async Task<OperatorSessionData> EnsureAuthenticatedAsync(CancellationToken cancellationToken)
    {
        await sessionSemaphore.WaitAsync(cancellationToken);
        try
        {
            var existing = sessionContext.CurrentSession ?? await sessionStore.LoadAsync();
            if (existing is not null)
            {
                if (existing.ExpiresAtUtc > DateTime.UtcNow.AddMinutes(1))
                {
                    var validated = await TryLoadProfileAsync(existing.AccessToken, existing.RefreshToken, existing.ExpiresAtUtc, cancellationToken);
                    if (validated is not null)
                    {
                        sessionContext.Set(validated);
                        return validated;
                    }
                }

                if (!string.IsNullOrWhiteSpace(existing.RefreshToken))
                {
                    var refreshed = await RefreshAsync(existing.RefreshToken, cancellationToken);
                    sessionContext.Set(refreshed);
                    await sessionStore.SaveAsync(refreshed);
                    return refreshed;
                }
            }

            var session = await LoginInteractiveAsync(cancellationToken);
            sessionContext.Set(session);
            await sessionStore.SaveAsync(session);
            return session;
        }
        finally
        {
            sessionSemaphore.Release();
        }
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var session = await EnsureAuthenticatedAsync(cancellationToken);
        return session.AccessToken;
    }

    public async Task<OperatorSessionData> ForceReauthenticateAsync(CancellationToken cancellationToken)
    {
        await sessionSemaphore.WaitAsync(cancellationToken);
        try
        {
            sessionContext.Clear();
            await sessionStore.ClearAsync();
        }
        finally
        {
            sessionSemaphore.Release();
        }

        return await EnsureAuthenticatedAsync(cancellationToken);
    }

    private async Task<OperatorSessionData> LoginInteractiveAsync(CancellationToken cancellationToken)
    {
        var state = Guid.NewGuid().ToString("N");
        var codeVerifier = CreateCodeVerifier();
        var codeChallenge = CreateCodeChallenge(codeVerifier);
        var authorizeUrl = BuildAuthorizeUrl(state, codeChallenge);

        using var listener = CreateListener();
        listener.Start();

        OpenBrowser(authorizeUrl);

        var callbackContext = await listener.GetContextAsync().WaitAsync(cancellationToken);
        try
        {
            var query = callbackContext.Request.QueryString;
            if (!string.Equals(query["state"], state, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Přihlášení bylo přerušeno, protože se neshoduje bezpečnostní stav požadavku.");
            }

            var error = query["error"];
            if (!string.IsNullOrWhiteSpace(error))
            {
                throw new InvalidOperationException($"OAuth přihlášení selhalo: {error}");
            }

            var code = query["code"];
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new InvalidOperationException("OAuth server nevrátil autorizační kód.");
            }

            await WriteBrowserResponseAsync(callbackContext.Response, "Přihlášení bylo úspěšné. Můžete zavřít toto okno a vrátit se do D3Bet.");
            return await ExchangeAuthorizationCodeAsync(code, codeVerifier, cancellationToken);
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task<OperatorSessionData> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{options.ServerBaseUrl.TrimEnd('/')}/connect/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = options.ClientId,
                ["refresh_token"] = refreshToken
            })
        };

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var tokenResponse = await DeserializeAsync<TokenResponse>(response, cancellationToken);
        return await CreateSessionAsync(tokenResponse, cancellationToken);
    }

    private async Task<OperatorSessionData> ExchangeAuthorizationCodeAsync(string code, string codeVerifier, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{options.ServerBaseUrl.TrimEnd('/')}/connect/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = options.ClientId,
                ["code"] = code,
                ["redirect_uri"] = options.RedirectUri,
                ["code_verifier"] = codeVerifier
            })
        };

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var tokenResponse = await DeserializeAsync<TokenResponse>(response, cancellationToken);
        return await CreateSessionAsync(tokenResponse, cancellationToken);
    }

    private async Task<OperatorSessionData> CreateSessionAsync(TokenResponse tokenResponse, CancellationToken cancellationToken)
    {
        var expiresAtUtc = DateTime.UtcNow.AddSeconds(Math.Max(tokenResponse.ExpiresIn, 60));
        return await LoadProfileAsync(tokenResponse.AccessToken, tokenResponse.RefreshToken, expiresAtUtc, cancellationToken);
    }

    private async Task<OperatorSessionData?> TryLoadProfileAsync(
        string accessToken,
        string? refreshToken,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            return await LoadProfileAsync(accessToken, refreshToken, expiresAtUtc, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private async Task<OperatorSessionData> LoadProfileAsync(
        string accessToken,
        string? refreshToken,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{options.ServerBaseUrl.TrimEnd('/')}/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var profile = await DeserializeAsync<OperatorProfileResponse>(response, cancellationToken);
        return new OperatorSessionData
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAtUtc = expiresAtUtc,
            UserId = profile.UserId,
            UserName = profile.UserName,
            Roles = profile.Roles.ToList()
        };
    }

    private string BuildAuthorizeUrl(string state, string codeChallenge)
    {
        var query = new Dictionary<string, string>
        {
            ["client_id"] = options.ClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = options.RedirectUri,
            ["scope"] = options.Scope,
            ["state"] = state,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256"
        };

        var queryString = string.Join("&", query.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));

        return $"{options.ServerBaseUrl.TrimEnd('/')}/connect/authorize?{queryString}";
    }

    private HttpListener CreateListener()
    {
        var listener = new HttpListener();
        listener.Prefixes.Add(options.RedirectUri);
        return listener;
    }

    private static void OpenBrowser(string url)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        };

        Process.Start(startInfo);
    }

    private static async Task WriteBrowserResponseAsync(HttpListenerResponse response, string message)
    {
        var bytes = Encoding.UTF8.GetBytes($"""
            <!DOCTYPE html>
            <html lang="cs">
            <head><meta charset="utf-8"><title>D3Bet Přihlášení</title></head>
            <body style="font-family:Segoe UI,Arial,sans-serif;background:#08111f;color:#f8fafc;display:flex;align-items:center;justify-content:center;min-height:100vh;margin:0;">
                <div style="max-width:480px;padding:28px;border-radius:20px;background:#0f172a;border:1px solid #334155;">
                    <div style="font-size:14px;font-weight:700;color:#f97316;">D3BET</div>
                    <h1 style="margin:10px 0 8px;">Přihlášení dokončeno</h1>
                    <p style="margin:0;color:#a7b6ca;line-height:1.6;">{WebUtility.HtmlEncode(message)}</p>
                </div>
            </body>
            </html>
            """);

        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.OutputStream.Close();
    }

    private static string CreateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64UrlEncode(bytes);
    }

    private static string CreateCodeChallenge(string codeVerifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static async Task<T> DeserializeAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
        return payload ?? throw new InvalidOperationException("Server vrátil prázdnou odpověď.");
    }

    private sealed class TokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;

        public string? RefreshToken { get; set; }

        public int ExpiresIn { get; set; }
    }

    private sealed class OperatorProfileResponse
    {
        public string UserId { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public List<string> Roles { get; set; } = [];
    }
}
