using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BettingApp.Wpf.ViewModels;

namespace BettingApp.Wpf.Services;

public sealed class LicenseService(OperatorAuthOptions authOptions)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private const string SharedSecret = "D3BET-LICENSING-LOCAL-SECRET-V1";
    private readonly HttpClient httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private string? licensePath;
    private string? installationPath;
    private string? clientKeyPath;
    private ClientLicenseState? currentLicense;

    public void ConfigurePaths(string appDataDirectory)
    {
        licensePath = Path.Combine(appDataDirectory, "license.key");
        installationPath = Path.Combine(appDataDirectory, "installation.id");
        clientKeyPath = Path.Combine(appDataDirectory, "license-client.key");
    }

    public async Task<ClientLicenseState> EnsureLicensedAsync(CancellationToken cancellationToken)
    {
        var existing = currentLicense ?? await LoadLicenseAsync(cancellationToken);
        if (existing is not null)
        {
            try
            {
                var validated = await ValidateOnlineAsync(existing, cancellationToken);
                currentLicense = validated;
                await SaveLicenseAsync(validated, cancellationToken);
                return validated;
            }
            catch (Exception ex) when (IsRecoverableConnectivityFailure(ex))
            {
                throw new InvalidOperationException("Nepodařilo se ověřit uloženou licenci kvůli spojení se serverem. Zkontrolujte připojení a zkuste to znovu.", ex);
            }
            catch
            {
                await DeleteStoredLicenseAsync(cancellationToken);
                currentLicense = null;
            }
        }

        var activated = await ActivateInteractiveAsync(cancellationToken);
        currentLicense = activated;
        await SaveLicenseAsync(activated, cancellationToken);
        return activated;
    }

    public async Task<string> GetLicenseTokenAsync(CancellationToken cancellationToken)
    {
        var license = await EnsureLicensedAsync(cancellationToken);
        return license.LicenseToken;
    }

    public async Task<string> GetInstallationIdAsync(CancellationToken cancellationToken)
    {
        EnsureConfigured();
        if (File.Exists(installationPath!))
        {
            return (await File.ReadAllTextAsync(installationPath!, cancellationToken)).Trim();
        }

        var installationId = $"CLI-{Guid.NewGuid():N}";
        await File.WriteAllTextAsync(installationPath!, installationId, cancellationToken);
        return installationId;
    }

    public string ComputeMachineFingerprint()
    {
        var raw = $"{Environment.MachineName}|{Environment.UserDomainName}|{Environment.OSVersion.VersionString}";
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }

    public async Task<DecryptedClientConfiguration> GetClientConfigurationAsync(CancellationToken cancellationToken)
    {
        var license = await EnsureLicensedAsync(cancellationToken);
        var installationId = await GetInstallationIdAsync(cancellationToken);

        return await ExecuteWithRetryAsync(async () =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{authOptions.ServerBaseUrl.TrimEnd('/')}/api/auth/client-configuration");
            request.Headers.Add("X-D3Bet-Bootstrap", license.BootstrapSessionToken);
            request.Headers.Add("X-D3Bet-InstallationId", installationId);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessOrThrowFriendlyAsync(response, cancellationToken);

            var encrypted = await response.Content.ReadFromJsonAsync<EncryptedClientConfigurationResponse>(SerializerOptions, cancellationToken)
                ?? throw new InvalidOperationException("Server nevrátil licenční konfiguraci klienta.");

            ValidateConfigurationSignature(encrypted);
            var configuration = DecryptConfiguration(encrypted, license.BootstrapSessionToken, installationId);
            configuration.ConfigId = encrypted.ConfigId;
            configuration.ConfigVersion = encrypted.ConfigVersion;
            configuration.IssuedAtUtc = encrypted.IssuedAtUtc;
            configuration.ExpiresAtUtc = encrypted.ExpiresAtUtc;
            configuration.BootstrapSessionToken = license.BootstrapSessionToken;

            license.CurrentConfigId = configuration.ConfigId;
            license.CurrentConfigVersion = configuration.ConfigVersion;
            license.CurrentConfigIssuedAtUtc = configuration.IssuedAtUtc;
            license.CurrentConfigExpiresAtUtc = configuration.ExpiresAtUtc;
            currentLicense = license;
            await SaveLicenseAsync(license, cancellationToken);
            return configuration;
        });
    }

    private async Task<ClientLicenseState?> LoadLicenseAsync(CancellationToken cancellationToken)
    {
        EnsureConfigured();
        if (!File.Exists(licensePath!))
        {
            return null;
        }

        var encrypted = await File.ReadAllBytesAsync(licensePath!, cancellationToken);
        var payload = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
        return JsonSerializer.Deserialize<ClientLicenseState>(payload, SerializerOptions);
    }

    private async Task SaveLicenseAsync(ClientLicenseState state, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var payload = JsonSerializer.SerializeToUtf8Bytes(state, SerializerOptions);
        var encrypted = ProtectedData.Protect(payload, null, DataProtectionScope.CurrentUser);
        await File.WriteAllBytesAsync(licensePath!, encrypted, cancellationToken);
    }

    private Task DeleteStoredLicenseAsync(CancellationToken cancellationToken)
    {
        EnsureConfigured();
        if (File.Exists(licensePath!))
        {
            File.Delete(licensePath!);
        }

        return Task.CompletedTask;
    }

    private async Task<ClientLicenseState> ValidateOnlineAsync(ClientLicenseState state, CancellationToken cancellationToken)
    {
        var installationId = await GetInstallationIdAsync(cancellationToken);
        var fingerprint = ComputeMachineFingerprint();
        using var response = await ExecuteWithRetryAsync(async () =>
            await httpClient.PostAsJsonAsync(
                $"{authOptions.ServerBaseUrl.TrimEnd('/')}/api/license/validate",
                new
                {
                    licenseToken = state.LicenseToken,
                    installationId,
                    machineFingerprint = fingerprint
                },
                cancellationToken));
        await EnsureSuccessOrThrowFriendlyAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<LicenseStatusResponse>(SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Server nevrátil stav licence.");
        return new ClientLicenseState
        {
            Email = payload.Email,
            LicenseToken = payload.LicenseToken,
            BootstrapSessionToken = payload.BootstrapSessionToken,
            BootstrapSessionExpiresAtUtc = payload.BootstrapSessionExpiresAtUtc,
            ConfirmationCode = payload.ConfirmationCode,
            ChallengeNonce = payload.ChallengeNonce,
            ChallengeExpiresAtUtc = payload.ChallengeExpiresAtUtc,
            InstallationId = payload.InstallationId,
            ServerInstanceId = payload.ServerInstanceId,
            CustomerName = payload.CustomerName,
            ExpiresAtUtc = payload.ExpiresAtUtc
        };
    }

    private async Task<ClientLicenseState> ActivateInteractiveAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var dialog = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var viewModel = new LicenseActivationViewModel(ActivateAsync);
            return new Views.LicenseActivationWindow(viewModel);
        });

        var dialogResult = await System.Windows.Application.Current.Dispatcher.InvokeAsync(dialog.ShowDialog);
        if (dialogResult != true || dialog.ViewModel.ActivatedLicense is null)
        {
            throw new InvalidOperationException("Bez aktivní licence nelze D3Bet klienta spustit.");
        }

        return dialog.ViewModel.ActivatedLicense;
    }

    private async Task<ClientLicenseState> ActivateAsync(string email, string activationKeyBase64, CancellationToken cancellationToken)
    {
        var installationId = await GetInstallationIdAsync(cancellationToken);
        var fingerprint = ComputeMachineFingerprint();
        var clientKeyMaterial = await GetOrCreateClientKeyMaterialAsync(cancellationToken);
        using var response = await ExecuteWithRetryAsync(async () =>
            await httpClient.PostAsJsonAsync(
                $"{authOptions.ServerBaseUrl.TrimEnd('/')}/api/license/activate",
                new
                {
                    email,
                    activationKeyBase64,
                    installationId,
                    machineFingerprint = fingerprint,
                    clientPublicKey = clientKeyMaterial.PublicKey
                },
                cancellationToken));
        await EnsureSuccessOrThrowFriendlyAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<LicenseStatusResponse>(SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Server nevrátil výsledek aktivace licence.");
        var state = new ClientLicenseState
        {
            Email = payload.Email,
            LicenseToken = payload.LicenseToken,
            BootstrapSessionToken = payload.BootstrapSessionToken,
            BootstrapSessionExpiresAtUtc = payload.BootstrapSessionExpiresAtUtc,
            ConfirmationCode = payload.ConfirmationCode,
            ChallengeNonce = payload.ChallengeNonce,
            ChallengeExpiresAtUtc = payload.ChallengeExpiresAtUtc,
            InstallationId = payload.InstallationId,
            ServerInstanceId = payload.ServerInstanceId,
            CustomerName = payload.CustomerName,
            ExpiresAtUtc = payload.ExpiresAtUtc
        };
        await ConfirmOnlineAsync(state, installationId, fingerprint, cancellationToken);
        return state;
    }

    private static DecryptedClientConfiguration DecryptConfiguration(
        EncryptedClientConfigurationResponse encrypted,
        string bootstrapSessionToken,
        string installationId)
    {
        var nonce = Convert.FromBase64String(encrypted.Nonce);
        var cipher = Convert.FromBase64String(encrypted.CipherText);
        var tag = Convert.FromBase64String(encrypted.Tag);
        var material = $"{SharedSecret}|{bootstrapSessionToken}|{installationId}|{Convert.ToBase64String(nonce)}";
        var key = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(key, 16);
        aes.Decrypt(nonce, cipher, tag, plain);
        return JsonSerializer.Deserialize<DecryptedClientConfiguration>(plain, SerializerOptions)
            ?? throw new InvalidOperationException("Licenční konfigurace klienta je poškozená.");
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(licensePath) || string.IsNullOrWhiteSpace(installationPath) || string.IsNullOrWhiteSpace(clientKeyPath))
        {
            throw new InvalidOperationException("LicenseService ještě nemá nastavené úložiště klienta.");
        }
    }

    private static async Task EnsureSuccessOrThrowFriendlyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await PreLoginErrorTranslator.EnsureSuccessOrThrowFriendlyAsync(
            response,
            cancellationToken,
            (detail, statusCode) => MapFriendlyLicenseError(detail, statusCode));
    }

    private static string MapFriendlyLicenseError(string detail, System.Net.HttpStatusCode statusCode)
    {
        if (statusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            return "Server licenci odmítl. Zkuste aktivaci zopakovat nebo kontaktujte správce D3Bet.";
        }

        if (detail.Contains("jiným zařízením klienta", StringComparison.OrdinalIgnoreCase))
        {
            return "Tahle licence už je navázaná na jiný počítač nebo profil Windows. Požádejte administrátora, aby licenci v D3Bet uvolnil pro nové zařízení.";
        }

        if (detail.Contains("jinou instalací klienta", StringComparison.OrdinalIgnoreCase))
        {
            return "Licence už byla použitá pro jinou instalaci klienta. Pokud jde o stejný počítač po reinstalaci, požádejte správce o znovu-navázání licence.";
        }

        if (detail.Contains("nejsou platné", StringComparison.OrdinalIgnoreCase))
        {
            return "Licenční e-mail nebo klíč nesouhlasí. Zkontrolujte prosím zadané údaje.";
        }

        if (detail.Contains("vypršela", StringComparison.OrdinalIgnoreCase))
        {
            return "Platnost licence už vypršela. Obraťte se na správce nebo podporu D3Bet.";
        }

        if (detail.Contains("zablokovaná", StringComparison.OrdinalIgnoreCase))
        {
            return "Licence je na serveru zablokovaná. Správce ji musí znovu povolit, než bude možné pokračovat.";
        }

        if (detail.Contains("bootstrap session", StringComparison.OrdinalIgnoreCase))
        {
            return "Bezpečné propojení klienta se serverem už vypršelo. Načtu prosím novou konfiguraci a zkuste to znovu.";
        }

        if (detail.Contains("bootstrap konfigurace", StringComparison.OrdinalIgnoreCase))
        {
            return "Dočasná přihlašovací konfigurace už není platná. Klient si musí vyžádat novou ze serveru.";
        }

        return detail;
    }

    private static void ValidateConfigurationSignature(EncryptedClientConfigurationResponse encrypted)
    {
        var payload = $"{encrypted.ConfigId}|{encrypted.ConfigVersion}|{encrypted.IssuedAtUtc:O}|{encrypted.ExpiresAtUtc:O}|{encrypted.Nonce}|{encrypted.CipherText}|{encrypted.Tag}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SharedSecret));
        var expected = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(encrypted.Signature)))
        {
            throw new InvalidOperationException("Bootstrap konfigurace klienta neprošla podpisovou kontrolou.");
        }
    }

    private async Task ConfirmOnlineAsync(ClientLicenseState state, string installationId, string fingerprint, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(state.ConfirmationCode) || string.IsNullOrWhiteSpace(state.ChallengeNonce))
        {
            return;
        }

        var clientKeyMaterial = await GetOrCreateClientKeyMaterialAsync(cancellationToken);
        var signature = SignChallenge(
            clientKeyMaterial.PrivateKey,
            BuildChallengePayload(state.LicenseToken, state.ConfirmationCode, state.ChallengeNonce, installationId, state.ServerInstanceId));

        using var response = await ExecuteWithRetryAsync(async () =>
            await httpClient.PostAsJsonAsync(
                $"{authOptions.ServerBaseUrl.TrimEnd('/')}/api/license/confirm",
                new
                {
                    licenseToken = state.LicenseToken,
                    installationId,
                    machineFingerprint = fingerprint,
                    confirmationCode = state.ConfirmationCode,
                    challengeSignature = signature
                },
                cancellationToken));
        await EnsureSuccessOrThrowFriendlyAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<LicenseStatusResponse>(SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Server nevrátil potvrzení licence.");
        state.BootstrapSessionToken = payload.BootstrapSessionToken;
        state.BootstrapSessionExpiresAtUtc = payload.BootstrapSessionExpiresAtUtc;
        state.ConfirmationCode = payload.ConfirmationCode;
        state.ChallengeNonce = payload.ChallengeNonce;
        state.ChallengeExpiresAtUtc = payload.ChallengeExpiresAtUtc;
        state.ExpiresAtUtc = payload.ExpiresAtUtc;
    }

    private static async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action)
    {
        Exception? lastException = null;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                return await action();
            }
            catch (Exception ex) when (IsRecoverableConnectivityFailure(ex) && attempt < 2)
            {
                lastException = ex;
                await Task.Delay(TimeSpan.FromMilliseconds(400 * (attempt + 1)));
            }
        }

        throw lastException ?? new InvalidOperationException("Požadovanou licenční operaci se nepodařilo dokončit.");
    }

    private static bool IsRecoverableConnectivityFailure(Exception exception) =>
        exception is HttpRequestException
        or TimeoutException
        or TaskCanceledException
        || exception is ApiClientException apiClientException && apiClientException.IsTransient;

    private async Task<ClientKeyMaterial> GetOrCreateClientKeyMaterialAsync(CancellationToken cancellationToken)
    {
        EnsureConfigured();
        if (File.Exists(clientKeyPath!))
        {
            var encrypted = await File.ReadAllBytesAsync(clientKeyPath!, cancellationToken);
            var payload = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            var existing = JsonSerializer.Deserialize<ClientKeyMaterial>(payload, SerializerOptions);
            if (existing is not null &&
                !string.IsNullOrWhiteSpace(existing.PublicKey) &&
                !string.IsNullOrWhiteSpace(existing.PrivateKey))
            {
                return existing;
            }
        }

        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var created = new ClientKeyMaterial
        {
            PublicKey = Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo()),
            PrivateKey = Convert.ToBase64String(ecdsa.ExportPkcs8PrivateKey())
        };

        var serialized = JsonSerializer.SerializeToUtf8Bytes(created, SerializerOptions);
        var protectedPayload = ProtectedData.Protect(serialized, null, DataProtectionScope.CurrentUser);
        await File.WriteAllBytesAsync(clientKeyPath!, protectedPayload, cancellationToken);
        return created;
    }

    private static string SignChallenge(string privateKeyBase64, string payload)
    {
        var data = Encoding.UTF8.GetBytes(payload);
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKeyBase64), out _);
        return Convert.ToBase64String(ecdsa.SignData(data, HashAlgorithmName.SHA256));
    }

    private static string BuildChallengePayload(
        string licenseToken,
        string confirmationCode,
        string challengeNonce,
        string installationId,
        string serverInstanceId) =>
        $"{licenseToken}|{confirmationCode}|{challengeNonce}|{installationId}|{serverInstanceId}";
}

public sealed class ClientLicenseState
{
    public string Email { get; set; } = string.Empty;

    public string LicenseToken { get; set; } = string.Empty;

    public string BootstrapSessionToken { get; set; } = string.Empty;

    public DateTime? BootstrapSessionExpiresAtUtc { get; set; }

    public string ConfirmationCode { get; set; } = string.Empty;

    public string ChallengeNonce { get; set; } = string.Empty;

    public DateTime? ChallengeExpiresAtUtc { get; set; }

    public string InstallationId { get; set; } = string.Empty;

    public string ServerInstanceId { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }

    public string CurrentConfigId { get; set; } = string.Empty;

    public int CurrentConfigVersion { get; set; }

    public DateTime? CurrentConfigIssuedAtUtc { get; set; }

    public DateTime? CurrentConfigExpiresAtUtc { get; set; }
}

public sealed class LicenseStatusResponse
{
    public bool IsValid { get; set; }

    public string Status { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string LicenseToken { get; set; } = string.Empty;

    public string BootstrapSessionToken { get; set; } = string.Empty;

    public DateTime? BootstrapSessionExpiresAtUtc { get; set; }

    public string ConfirmationCode { get; set; } = string.Empty;

    public string ChallengeNonce { get; set; } = string.Empty;

    public DateTime? ChallengeExpiresAtUtc { get; set; }

    public string Email { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public string ServerInstanceId { get; set; } = string.Empty;

    public string InstallationId { get; set; } = string.Empty;

    public DateTime IssuedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }
}

public sealed class EncryptedClientConfigurationResponse
{
    public string ConfigId { get; set; } = string.Empty;

    public int ConfigVersion { get; set; }

    public DateTime IssuedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public string Nonce { get; set; } = string.Empty;

    public string CipherText { get; set; } = string.Empty;

    public string Tag { get; set; } = string.Empty;

    public string Algorithm { get; set; } = string.Empty;

    public string KeyVersion { get; set; } = string.Empty;

    public string Signature { get; set; } = string.Empty;
}

public sealed class DecryptedClientConfiguration
{
    public string ConfigId { get; set; } = string.Empty;

    public int ConfigVersion { get; set; }

    public DateTime IssuedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public string BootstrapSessionToken { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string RedirectUri { get; set; } = string.Empty;

    public string Scope { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
}

public sealed class ClientKeyMaterial
{
    public string PublicKey { get; set; } = string.Empty;

    public string PrivateKey { get; set; } = string.Empty;
}
