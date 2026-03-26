using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using BettingApp.Wpf.ViewModels;
using BettingApp.Wpf.Views;

namespace BettingApp.Wpf.Services;

public sealed class OperatorAuthService(
    OperatorAuthOptions options,
    SelfServiceApiClient selfServiceApiClient,
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

    public async Task<OperatorSessionData> RefreshCurrentSessionAsync(CancellationToken cancellationToken)
    {
        var current = sessionContext.CurrentSession ?? await sessionStore.LoadAsync();
        if (current is null)
        {
            throw new InvalidOperationException("Není k dispozici aktivní relace k obnovení profilu.");
        }

        var refreshed = await LoadProfileAsync(current.AccessToken, current.RefreshToken, current.ExpiresAtUtc, cancellationToken);
        sessionContext.Set(refreshed);
        await sessionStore.SaveAsync(refreshed);
        return refreshed;
    }

    public async Task<OperatorSessionData?> TryRefreshSessionSilentlyAsync(CancellationToken cancellationToken)
    {
        await sessionSemaphore.WaitAsync(cancellationToken);
        try
        {
            var current = sessionContext.CurrentSession ?? await sessionStore.LoadAsync();
            if (current is null)
            {
                return null;
            }

            var validated = await TryLoadProfileAsync(current.AccessToken, current.RefreshToken, current.ExpiresAtUtc, cancellationToken);
            if (validated is not null)
            {
                sessionContext.Set(validated);
                await sessionStore.SaveAsync(validated);
                return validated;
            }

            if (string.IsNullOrWhiteSpace(current.RefreshToken))
            {
                return null;
            }

            try
            {
                var refreshed = await RefreshAsync(current.RefreshToken, cancellationToken);
                sessionContext.Set(refreshed);
                await sessionStore.SaveAsync(refreshed);
                return refreshed;
            }
            catch
            {
                return null;
            }
        }
        finally
        {
            sessionSemaphore.Release();
        }
    }

    private async Task<OperatorSessionData> LoginInteractiveAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var dialog = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var viewModel = new LoginViewModel(selfServiceApiClient, AuthenticateWithPasswordAsync);
            var window = new LoginWindow(viewModel);
            var owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive);
            if (owner is not null)
            {
                window.Owner = owner;
            }

            return window;
        });

        var dialogResult = await System.Windows.Application.Current.Dispatcher.InvokeAsync(dialog.ShowDialog);
        if (dialogResult != true || dialog.ViewModel.AuthenticatedSession is null)
        {
            throw new InvalidOperationException("Přihlášení bylo zrušeno.");
        }

        return dialog.ViewModel.AuthenticatedSession;
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

    private async Task<OperatorSessionData> AuthenticateWithPasswordAsync(string userName, string password, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{options.ServerBaseUrl.TrimEnd('/')}/connect/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = options.ClientId,
                ["username"] = userName,
                ["password"] = password,
                ["scope"] = options.Scope
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

    private static async Task<T> DeserializeAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
        return payload ?? throw new InvalidOperationException("Server vrátil prázdnou odpověď.");
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    private sealed class OperatorProfileResponse
    {
        public string UserId { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public List<string> Roles { get; set; } = [];
    }
}
