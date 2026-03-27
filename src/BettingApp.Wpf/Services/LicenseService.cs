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
    private ClientLicenseState? currentLicense;

    public void ConfigurePaths(string appDataDirectory)
    {
        licensePath = Path.Combine(appDataDirectory, "license.key");
        installationPath = Path.Combine(appDataDirectory, "installation.id");
    }

    public async Task<ClientLicenseState> EnsureLicensedAsync(CancellationToken cancellationToken)
    {
        var existing = currentLicense ?? await LoadLicenseAsync(cancellationToken);
        if (existing is not null)
        {
            var validated = await ValidateOnlineAsync(existing, cancellationToken);
            currentLicense = validated;
            await SaveLicenseAsync(validated, cancellationToken);
            return validated;
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
        var fingerprint = ComputeMachineFingerprint();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{authOptions.ServerBaseUrl.TrimEnd('/')}/api/auth/client-configuration");
        request.Headers.Add("X-D3Bet-License", license.LicenseToken);
        request.Headers.Add("X-D3Bet-InstallationId", installationId);
        request.Headers.Add("X-D3Bet-Fingerprint", fingerprint);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var encrypted = await response.Content.ReadFromJsonAsync<EncryptedClientConfigurationResponse>(SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Server nevrátil licenční konfiguraci klienta.");

        return DecryptConfiguration(encrypted, license.LicenseToken, installationId);
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

    private async Task<ClientLicenseState> ValidateOnlineAsync(ClientLicenseState state, CancellationToken cancellationToken)
    {
        var installationId = await GetInstallationIdAsync(cancellationToken);
        var fingerprint = ComputeMachineFingerprint();
        using var response = await httpClient.PostAsJsonAsync(
            $"{authOptions.ServerBaseUrl.TrimEnd('/')}/api/license/validate",
            new
            {
                licenseToken = state.LicenseToken,
                installationId,
                machineFingerprint = fingerprint
            },
            cancellationToken);
        await EnsureSuccessOrThrowFriendlyAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<LicenseStatusResponse>(SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Server nevrátil stav licence.");
        return new ClientLicenseState
        {
            Email = payload.Email,
            LicenseToken = payload.LicenseToken,
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
        using var response = await httpClient.PostAsJsonAsync(
            $"{authOptions.ServerBaseUrl.TrimEnd('/')}/api/license/activate",
            new
            {
                email,
                activationKeyBase64,
                installationId,
                machineFingerprint = fingerprint
            },
            cancellationToken);
        await EnsureSuccessOrThrowFriendlyAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<LicenseStatusResponse>(SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Server nevrátil výsledek aktivace licence.");
        return new ClientLicenseState
        {
            Email = payload.Email,
            LicenseToken = payload.LicenseToken,
            InstallationId = payload.InstallationId,
            ServerInstanceId = payload.ServerInstanceId,
            CustomerName = payload.CustomerName,
            ExpiresAtUtc = payload.ExpiresAtUtc
        };
    }

    private static DecryptedClientConfiguration DecryptConfiguration(
        EncryptedClientConfigurationResponse encrypted,
        string licenseToken,
        string installationId)
    {
        var nonce = Convert.FromBase64String(encrypted.Nonce);
        var cipher = Convert.FromBase64String(encrypted.CipherText);
        var tag = Convert.FromBase64String(encrypted.Tag);
        var material = $"{SharedSecret}|{licenseToken}|{installationId}|{Convert.ToBase64String(nonce)}";
        var key = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(key, 16);
        aes.Decrypt(nonce, cipher, tag, plain);
        return JsonSerializer.Deserialize<DecryptedClientConfiguration>(plain, SerializerOptions)
            ?? throw new InvalidOperationException("Licenční konfigurace klienta je poškozená.");
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(licensePath) || string.IsNullOrWhiteSpace(installationPath))
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

        return detail;
    }
}

public sealed class ClientLicenseState
{
    public string Email { get; set; } = string.Empty;

    public string LicenseToken { get; set; } = string.Empty;

    public string InstallationId { get; set; } = string.Empty;

    public string ServerInstanceId { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }
}

public sealed class LicenseStatusResponse
{
    public bool IsValid { get; set; }

    public string Status { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string LicenseToken { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public string ServerInstanceId { get; set; } = string.Empty;

    public string InstallationId { get; set; } = string.Empty;

    public DateTime IssuedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }
}

public sealed class EncryptedClientConfigurationResponse
{
    public string Nonce { get; set; } = string.Empty;

    public string CipherText { get; set; } = string.Empty;

    public string Tag { get; set; } = string.Empty;

    public string Algorithm { get; set; } = string.Empty;

    public string KeyVersion { get; set; } = string.Empty;
}

public sealed class DecryptedClientConfiguration
{
    public string ClientId { get; set; } = string.Empty;

    public string RedirectUri { get; set; } = string.Empty;

    public string Scope { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
}
