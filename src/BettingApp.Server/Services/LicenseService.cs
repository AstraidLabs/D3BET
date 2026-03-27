using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BettingApp.Server.Configuration;
using BettingApp.Server.Models;
using Microsoft.Extensions.Options;

namespace BettingApp.Server.Services;

public sealed class LicenseService(
    LicenseStore licenseStore,
    IOptions<LicensingOptions> licensingOptions,
    IOptions<OperatorOAuthClientOptions> operatorOAuthOptions)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly LicensingOptions options = licensingOptions.Value;

    public async Task<LicenseStatusResponse> ActivateAsync(LicenseActivationRequest request, CancellationToken cancellationToken)
    {
        ValidateActivationRequest(request);

        var bootstrap = options.BootstrapLicenses.FirstOrDefault(item =>
            string.Equals(item.Email.Trim(), request.Email.Trim(), StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.ActivationKeyBase64.Trim(), request.ActivationKeyBase64.Trim(), StringComparison.Ordinal));

        if (bootstrap is null)
        {
            throw new InvalidOperationException("Licenční klíč nebo e-mail nejsou platné.");
        }

        var state = NormalizeState(await licenseStore.LoadAsync(cancellationToken));
        var activationKeyHash = ComputeSha256(request.ActivationKeyBase64.Trim());
        var machineFingerprintHash = ComputeSha256(request.MachineFingerprint.Trim());

        var existing = state.Licenses.FirstOrDefault(item => item.ActivationKeyHash == activationKeyHash);
        if (existing is not null &&
            !string.Equals(existing.InstallationId, request.InstallationId.Trim(), StringComparison.Ordinal))
        {
            if (!string.Equals(existing.MachineFingerprintHash, machineFingerprintHash, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Tato licence už je spárovaná s jiným zařízením klienta.");
            }
        }

        var issuedAtUtc = DateTime.UtcNow;
        var expiresAtUtc = issuedAtUtc.AddDays(Math.Max(options.LicenseValidityDays, 30));
        var binding = new LicenseBindingRecord(
            existing?.LicenseId ?? $"LIC-{Guid.NewGuid():N}",
            bootstrap.Email.Trim(),
            string.IsNullOrWhiteSpace(bootstrap.CustomerName) ? bootstrap.Email.Trim() : bootstrap.CustomerName.Trim(),
            activationKeyHash,
            request.InstallationId.Trim(),
            machineFingerprintHash,
            existing?.IssuedAtUtc ?? issuedAtUtc,
            expiresAtUtc,
            false);

        var updated = state.Licenses
            .Where(item => item.ActivationKeyHash != activationKeyHash)
            .Append(binding)
            .ToArray();

        var message = existing is not null &&
            !string.Equals(existing.InstallationId, request.InstallationId.Trim(), StringComparison.Ordinal)
            ? "Licence byla znovu navázaná pro tuto instalaci klienta."
            : "Licence byla úspěšně aktivovaná.";

        var auditEntries = AppendAudit(
            state.AuditEntries,
            new LicenseAuditEntryRecord(
                Guid.NewGuid(),
                DateTime.UtcNow,
                binding.LicenseId,
                existing is null ? "activated" : "rebound",
                $"{message} ({binding.Email})",
                binding.Email,
                binding.InstallationId,
                true));

        await licenseStore.SaveAsync(new LicenseStoreState(state.ServerInstanceId, updated, auditEntries), cancellationToken);

        var token = CreateLicenseToken(binding, state.ServerInstanceId);
        return CreateStatusResponse(binding, state.ServerInstanceId, token, "Active", message);
    }

    public async Task<LicenseStatusResponse> ValidateAsync(LicenseValidationRequest request, CancellationToken cancellationToken)
    {
        var validation = await ValidateTokenCoreAsync(request.LicenseToken, request.InstallationId, request.MachineFingerprint, cancellationToken);
        return CreateStatusResponse(validation.Binding, validation.ServerInstanceId, request.LicenseToken, "Active", "Licence je platná a spárovaná se serverem.");
    }

    public async Task<ValidatedLicenseContext> ValidateRequestAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var licenseToken = request.Headers["X-D3Bet-License"].FirstOrDefault();
        var installationId = request.Headers["X-D3Bet-InstallationId"].FirstOrDefault();
        var machineFingerprint = request.Headers["X-D3Bet-Fingerprint"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(licenseToken) ||
            string.IsNullOrWhiteSpace(installationId) ||
            string.IsNullOrWhiteSpace(machineFingerprint))
        {
            throw new InvalidOperationException("Klient neposlal platné licenční údaje.");
        }

        return await ValidateTokenCoreAsync(licenseToken, installationId, machineFingerprint, cancellationToken);
    }

    public async Task<EncryptedClientConfigurationResponse> BuildEncryptedClientConfigurationAsync(
        ValidatedLicenseContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var plainPayload = JsonSerializer.SerializeToUtf8Bytes(new OperatorClientConfigurationResponse(
            operatorOAuthOptions.Value.ClientId,
            operatorOAuthOptions.Value.RedirectUri,
            "openid profile roles offline_access operations",
            operatorOAuthOptions.Value.DisplayName));

        var nonce = RandomNumberGenerator.GetBytes(12);
        var key = DeriveConfigurationKey(context.LicenseToken, context.Binding.InstallationId, nonce);
        var cipher = new byte[plainPayload.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, plainPayload, cipher, tag);

        return new EncryptedClientConfigurationResponse(
            Convert.ToBase64String(nonce),
            Convert.ToBase64String(cipher),
            Convert.ToBase64String(tag),
            "AES-256-GCM",
            "v1");
    }

    private async Task<ValidatedLicenseContext> ValidateTokenCoreAsync(
        string licenseToken,
        string installationId,
        string machineFingerprint,
        CancellationToken cancellationToken)
    {
        var normalizedToken = licenseToken.Trim();
        var parts = normalizedToken.Split('.', 2);
        if (parts.Length != 2)
        {
            throw new InvalidOperationException("Licence klienta má neplatný formát.");
        }

        var expectedSignature = ComputeHmac(parts[0]);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedSignature),
                Encoding.UTF8.GetBytes(parts[1])))
        {
            throw new InvalidOperationException("Podpis licence není platný.");
        }

        var payloadJson = Encoding.UTF8.GetString(Convert.FromBase64String(parts[0]));
        var payload = JsonSerializer.Deserialize<LicenseTokenPayload>(payloadJson, SerializerOptions)
            ?? throw new InvalidOperationException("Licenční token je poškozený.");

        var state = NormalizeState(await licenseStore.LoadAsync(cancellationToken));
        if (!string.Equals(payload.ServerInstanceId, state.ServerInstanceId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Licence nepatří k tomuto serveru.");
        }

        var binding = state.Licenses.FirstOrDefault(item => item.LicenseId == payload.LicenseId)
            ?? throw new InvalidOperationException("Licence už na serveru neexistuje.");

        if (binding.IsRevoked)
        {
            throw new InvalidOperationException("Licence byla na serveru zablokovaná.");
        }

        if (binding.ExpiresAtUtc <= DateTime.UtcNow)
        {
            throw new InvalidOperationException("Licence už vypršela.");
        }

        var normalizedInstallationId = installationId.Trim();
        var normalizedFingerprintHash = ComputeSha256(machineFingerprint.Trim());

        if (!string.IsNullOrWhiteSpace(binding.InstallationId) &&
            !string.Equals(binding.InstallationId, normalizedInstallationId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Licence nepatří k této instalaci klienta.");
        }

        if (!string.IsNullOrWhiteSpace(binding.MachineFingerprintHash) &&
            !string.Equals(binding.MachineFingerprintHash, normalizedFingerprintHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Licence neodpovídá tomuto zařízení.");
        }

        var refreshedBinding = binding with
        {
            InstallationId = string.IsNullOrWhiteSpace(binding.InstallationId) ? normalizedInstallationId : binding.InstallationId,
            MachineFingerprintHash = string.IsNullOrWhiteSpace(binding.MachineFingerprintHash) ? normalizedFingerprintHash : binding.MachineFingerprintHash,
            LastValidatedAtUtc = DateTime.UtcNow
        };

        var updatedLicenses = state.Licenses
            .Select(item => item.LicenseId == refreshedBinding.LicenseId ? refreshedBinding : item)
            .ToArray();
        var auditEntries = AppendAudit(
            state.AuditEntries,
            new LicenseAuditEntryRecord(
                Guid.NewGuid(),
                DateTime.UtcNow,
                refreshedBinding.LicenseId,
                "validated",
                $"Licence pro {refreshedBinding.Email} byla ověřená klientem.",
                refreshedBinding.Email,
                refreshedBinding.InstallationId,
                true));
        await licenseStore.SaveAsync(new LicenseStoreState(state.ServerInstanceId, updatedLicenses, auditEntries), cancellationToken);

        return new ValidatedLicenseContext(refreshedBinding, state.ServerInstanceId, licenseToken);
    }

    private string CreateLicenseToken(LicenseBindingRecord binding, string serverInstanceId)
    {
        var payload = new LicenseTokenPayload(
            binding.LicenseId,
            binding.Email,
            binding.CustomerName,
            serverInstanceId,
            binding.InstallationId,
            binding.MachineFingerprintHash,
            binding.IssuedAtUtc,
            binding.ExpiresAtUtc);

        var payloadJson = JsonSerializer.Serialize(payload, SerializerOptions);
        var payloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson));
        var signature = ComputeHmac(payloadBase64);
        return $"{payloadBase64}.{signature}";
    }

    private LicenseStatusResponse CreateStatusResponse(
        LicenseBindingRecord binding,
        string serverInstanceId,
        string token,
        string status,
        string message)
    {
        return new LicenseStatusResponse(
            true,
            status,
            message,
            token,
            binding.Email,
            binding.CustomerName,
            serverInstanceId,
            binding.InstallationId,
            binding.IssuedAtUtc,
            binding.ExpiresAtUtc);
    }

    private void ValidateActivationRequest(LicenseActivationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            !request.Email.Contains('@', StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Zadejte platný e-mail k licenci.");
        }

        if (string.IsNullOrWhiteSpace(request.ActivationKeyBase64))
        {
            throw new InvalidOperationException("Zadejte licenční klíč.");
        }

        try
        {
            _ = Convert.FromBase64String(request.ActivationKeyBase64.Trim());
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("Licenční klíč musí být ve formátu Base64.");
        }

        if (string.IsNullOrWhiteSpace(request.InstallationId))
        {
            throw new InvalidOperationException("Klient neposlal platné ID instalace.");
        }

        if (string.IsNullOrWhiteSpace(request.MachineFingerprint))
        {
            throw new InvalidOperationException("Klient neposlal platný fingerprint zařízení.");
        }
    }

    private byte[] DeriveConfigurationKey(string licenseToken, string installationId, byte[] nonce)
    {
        var material = $"{options.SharedSecret}|{licenseToken}|{installationId}|{Convert.ToBase64String(nonce)}";
        return SHA256.HashData(Encoding.UTF8.GetBytes(material));
    }

    private string ComputeHmac(string payloadBase64)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(options.SharedSecret));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadBase64)));
    }

    private static string ComputeSha256(string value)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static LicenseStoreState NormalizeState(LicenseStoreState state) =>
        state with { AuditEntries = state.AuditEntries ?? [] };

    private static IReadOnlyList<LicenseAuditEntryRecord> AppendAudit(
        IReadOnlyList<LicenseAuditEntryRecord>? source,
        LicenseAuditEntryRecord entry)
    {
        return (source ?? [])
            .Append(entry)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(200)
            .ToArray();
    }
}

public sealed record ValidatedLicenseContext(LicenseBindingRecord Binding, string ServerInstanceId, string LicenseToken);

internal sealed record LicenseTokenPayload(
    string LicenseId,
    string Email,
    string CustomerName,
    string ServerInstanceId,
    string InstallationId,
    string MachineFingerprintHash,
    DateTime IssuedAtUtc,
    DateTime ExpiresAtUtc);
