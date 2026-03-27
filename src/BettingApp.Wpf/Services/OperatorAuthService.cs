using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using BettingApp.Wpf.ViewModels;
using BettingApp.Wpf.Views;

namespace BettingApp.Wpf.Services;

public sealed class OperatorAuthService(
    OperatorAuthOptions options,
    LicenseService licenseService,
    SelfServiceApiClient selfServiceApiClient,
    OperatorSessionStore sessionStore,
    OperatorSessionContext sessionContext)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim sessionSemaphore = new(1, 1);
    private OAuthClientConfiguration? resolvedClientConfiguration;
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
                if (!string.IsNullOrWhiteSpace(existing.RefreshToken))
                {
                    var refreshed = await TryRefreshSessionSilentlyCore(existing, cancellationToken);
                    if (refreshed is not null)
                    {
                        sessionContext.Set(refreshed);
                        await sessionStore.SaveAsync(refreshed);
                        return refreshed;
                    }
                }

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

            var refreshed = await TryRefreshSessionSilentlyCore(current, cancellationToken);
            if (refreshed is not null)
            {
                sessionContext.Set(refreshed);
                await sessionStore.SaveAsync(refreshed);
                return refreshed;
            }

            return null;
        }
        finally
        {
            sessionSemaphore.Release();
        }
    }

    private async Task<OperatorSessionData?> TryRefreshSessionSilentlyCore(OperatorSessionData current, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(current.RefreshToken))
        {
            try
            {
                return await RefreshAsync(current.RefreshToken, cancellationToken);
            }
            catch
            {
            }
        }

        var validated = await TryLoadProfileAsync(current.AccessToken, current.RefreshToken, current.ExpiresAtUtc, cancellationToken);
        if (validated is not null)
        {
            return validated;
        }

        if (string.IsNullOrWhiteSpace(current.RefreshToken))
        {
            return null;
        }

        return null;
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
        var clientConfiguration = await GetOAuthClientConfigurationAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{options.ServerBaseUrl.TrimEnd('/')}/connect/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = clientConfiguration.ClientId,
                ["refresh_token"] = refreshToken
            })
        };

        using var response = await httpClient.SendAsync(request, cancellationToken);
        await PreLoginErrorTranslator.EnsureSuccessOrThrowFriendlyAsync(
            response,
            cancellationToken,
            static (detail, statusCode) =>
            {
                if (statusCode == System.Net.HttpStatusCode.BadRequest || statusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    if (detail.Contains("Neplatné přihlašovací údaje", StringComparison.OrdinalIgnoreCase))
                    {
                        return "Uživatelské jméno nebo heslo nesouhlasí. Zkuste je prosím zadat znovu.";
                    }

                    if (detail.Contains("Účet ještě není aktivovaný", StringComparison.OrdinalIgnoreCase))
                    {
                        return "Účet ještě není aktivovaný nebo je dočasně mimo provoz. Dokončete aktivaci nebo kontaktujte správce.";
                    }

                    return "Obnovení přihlášení se nepodařilo. Přihlaste se prosím znovu.";
                }

                return string.IsNullOrWhiteSpace(detail)
                    ? "Server teď neumožnil obnovit relaci. Přihlaste se prosím znovu."
                    : detail;
            });
        var tokenResponse = await DeserializeAsync<TokenResponse>(response, cancellationToken);
        return await CreateSessionAsync(tokenResponse, cancellationToken);
    }

    private async Task<OperatorSessionData> AuthenticateWithPasswordAsync(string userName, string password, CancellationToken cancellationToken)
    {
        var clientConfiguration = await GetOAuthClientConfigurationAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{options.ServerBaseUrl.TrimEnd('/')}/connect/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = clientConfiguration.ClientId,
                ["username"] = userName,
                ["password"] = password,
                ["scope"] = clientConfiguration.Scope
            })
        };

        using var response = await httpClient.SendAsync(request, cancellationToken);
        await PreLoginErrorTranslator.EnsureSuccessOrThrowFriendlyAsync(
            response,
            cancellationToken,
            static (detail, statusCode) =>
            {
                if (statusCode == System.Net.HttpStatusCode.BadRequest || statusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    if (detail.Contains("Neplatné přihlašovací údaje", StringComparison.OrdinalIgnoreCase))
                    {
                        return "Uživatelské jméno nebo heslo nesouhlasí. Zkuste je prosím zadat znovu.";
                    }

                    if (detail.Contains("Účet ještě není aktivovaný", StringComparison.OrdinalIgnoreCase))
                    {
                        return "Účet ještě není aktivovaný nebo je dočasně mimo provoz. Dokončete aktivaci nebo kontaktujte správce.";
                    }

                    return "Přihlášení se nepodařilo dokončit. Zkontrolujte zadané údaje a stav účtu.";
                }

                return string.IsNullOrWhiteSpace(detail)
                    ? "Přihlášení se nepodařilo dokončit. Zkuste to prosím znovu."
                    : detail;
            });
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
        await PreLoginErrorTranslator.EnsureSuccessOrThrowFriendlyAsync(
            response,
            cancellationToken,
            static (detail, statusCode) =>
            {
                if (statusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return "Relace už není platná. Přihlaste se prosím znovu.";
                }

                return string.IsNullOrWhiteSpace(detail)
                    ? "Nepodařilo se načíst profil přihlášeného účtu."
                    : detail;
            });

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

    private async Task<OAuthClientConfiguration> GetOAuthClientConfigurationAsync(CancellationToken cancellationToken)
    {
        if (resolvedClientConfiguration is not null)
        {
            return resolvedClientConfiguration;
        }

        try
        {
            var licenseToken = await licenseService.GetLicenseTokenAsync(cancellationToken);
            var installationId = await licenseService.GetInstallationIdAsync(cancellationToken);
            var fingerprint = licenseService.ComputeMachineFingerprint();

            using var request = new HttpRequestMessage(HttpMethod.Get, $"{options.ServerBaseUrl.TrimEnd('/')}/api/auth/client-configuration");
            request.Headers.Add("X-D3Bet-License", licenseToken);
            request.Headers.Add("X-D3Bet-InstallationId", installationId);
            request.Headers.Add("X-D3Bet-Fingerprint", fingerprint);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            await PreLoginErrorTranslator.EnsureSuccessOrThrowFriendlyAsync(
                response,
                cancellationToken,
                static (detail, statusCode) =>
                {
                    if (statusCode == System.Net.HttpStatusCode.BadRequest || statusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        return "Nepodařilo se načíst zabezpečenou konfiguraci klienta. Zkontrolujte platnost licence a zkuste to znovu.";
                    }

                    return string.IsNullOrWhiteSpace(detail)
                        ? "Server teď neposkytl konfiguraci pro přihlášení klienta."
                        : detail;
                });
            var encrypted = await DeserializeAsync<EncryptedClientConfigurationResponse>(response, cancellationToken);
            var decrypted = DecryptConfiguration(encrypted, licenseToken, installationId);
            var configuration = new OAuthClientConfiguration
            {
                ClientId = decrypted.ClientId,
                RedirectUri = decrypted.RedirectUri,
                Scope = decrypted.Scope,
                DisplayName = decrypted.DisplayName
            };
            resolvedClientConfiguration = configuration;
            return configuration;
        }
        catch
        {
            resolvedClientConfiguration = new OAuthClientConfiguration
            {
                ClientId = options.ClientId,
                RedirectUri = options.RedirectUri,
                Scope = options.Scope,
                DisplayName = "D3Bet Operator Client"
            };
            return resolvedClientConfiguration;
        }
    }

    private static DecryptedClientConfiguration DecryptConfiguration(
        EncryptedClientConfigurationResponse encrypted,
        string licenseToken,
        string installationId)
    {
        var nonce = Convert.FromBase64String(encrypted.Nonce);
        var cipher = Convert.FromBase64String(encrypted.CipherText);
        var tag = Convert.FromBase64String(encrypted.Tag);
        var material = $"D3BET-LICENSING-LOCAL-SECRET-V1|{licenseToken}|{installationId}|{Convert.ToBase64String(nonce)}";
        var key = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(key, 16);
        aes.Decrypt(nonce, cipher, tag, plain);
        return JsonSerializer.Deserialize<DecryptedClientConfiguration>(plain, JsonOptions)
            ?? throw new InvalidOperationException("Zašifrovaná konfigurace klienta je poškozená.");
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

    private sealed class OAuthClientConfiguration
    {
        public string ClientId { get; set; } = string.Empty;

        public string RedirectUri { get; set; } = string.Empty;

        public string Scope { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;
    }
}
