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
    private static readonly TimeSpan PendingConfirmationWindow = TimeSpan.FromMinutes(10);
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
        ValidateClientPublicKey(request.ClientPublicKey);

        var existing = state.Licenses.FirstOrDefault(item => item.ActivationKeyHash == activationKeyHash);
        if (existing is not null &&
            existing.IsConfirmed &&
            !string.Equals(existing.InstallationId, request.InstallationId.Trim(), StringComparison.Ordinal) &&
            !string.Equals(existing.MachineFingerprintHash, machineFingerprintHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Tato licence už je spárovaná s jiným zařízením klienta.");
        }

        var issuedAtUtc = DateTime.UtcNow;
        var expiresAtUtc = issuedAtUtc.AddDays(Math.Max(options.LicenseValidityDays, 30));
        var confirmationCode = existing?.PendingConfirmationCode;
        if (string.IsNullOrWhiteSpace(confirmationCode) ||
            existing?.PendingActivatedAtUtc is null ||
            existing.PendingActivatedAtUtc.Value.Add(PendingConfirmationWindow) <= issuedAtUtc)
        {
            confirmationCode = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));
        }

        var challengeNonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
        var challengeExpiresAtUtc = issuedAtUtc.Add(PendingConfirmationWindow);
        var normalizedInstallationId = request.InstallationId.Trim();
        var binding = new LicenseBindingRecord(
            existing?.LicenseId ?? $"LIC-{Guid.NewGuid():N}",
            bootstrap.Email.Trim(),
            string.IsNullOrWhiteSpace(bootstrap.CustomerName) ? bootstrap.Email.Trim() : bootstrap.CustomerName.Trim(),
            activationKeyHash,
            normalizedInstallationId,
            machineFingerprintHash,
            existing?.IssuedAtUtc ?? issuedAtUtc,
            expiresAtUtc,
            false,
            existing?.LastValidatedAtUtc,
            false,
            confirmationCode,
            issuedAtUtc,
            existing?.ConfirmedAtUtc,
            request.ClientPublicKey.Trim(),
            challengeNonce,
            challengeExpiresAtUtc);

        var licenses = state.Licenses
            .Where(item => item.ActivationKeyHash != activationKeyHash)
            .Append(binding)
            .ToArray();

        var bootstrapSessions = RevokeBootstrapSessions(
            state.BootstrapSessions,
            item => string.Equals(item.LicenseId, binding.LicenseId, StringComparison.Ordinal));
        var bootstrapConfigurations = RevokeBootstrapConfigurations(
            state.BootstrapConfigurations,
            item => string.Equals(item.LicenseId, binding.LicenseId, StringComparison.Ordinal));

        var message = existing is not null &&
            !string.Equals(existing.InstallationId, normalizedInstallationId, StringComparison.Ordinal)
            ? "Licence byla znovu navázaná pro tuto instalaci klienta."
            : "Licence byla úspěšně aktivovaná.";

        var auditEntries = AppendAudit(
            state.AuditEntries,
            new LicenseAuditEntryRecord(
                Guid.NewGuid(),
                DateTime.UtcNow,
                binding.LicenseId,
                existing is null ? "activation_pending" : "rebound_pending",
                $"{message} ({binding.Email}) Aktivace čeká na potvrzení klientem.",
                binding.Email,
                binding.InstallationId,
                true));

        await SaveStateAsync(state.ServerInstanceId, licenses, bootstrapSessions, bootstrapConfigurations, auditEntries, cancellationToken);

        var token = CreateLicenseToken(binding, state.ServerInstanceId);
        return CreateStatusResponse(binding, state.ServerInstanceId, token, null, null, "PendingConfirmation", message);
    }

    public async Task<LicenseStatusResponse> ValidateAsync(LicenseValidationRequest request, CancellationToken cancellationToken)
    {
        var validation = await ValidateTokenCoreAsync(request.LicenseToken, request.InstallationId, request.MachineFingerprint, confirmLicense: false, cancellationToken);
        BootstrapSessionContext? bootstrapSession = null;

        if (validation.Binding.IsConfirmed)
        {
            bootstrapSession = await IssueBootstrapSessionAsync(validation.Binding, validation.ServerInstanceId, cancellationToken);
        }

        return CreateStatusResponse(
            validation.Binding,
            validation.ServerInstanceId,
            request.LicenseToken,
            bootstrapSession?.SessionToken,
            bootstrapSession?.Session.ExpiresAtUtc,
            validation.Binding.IsConfirmed ? "Active" : "PendingConfirmation",
            validation.Binding.IsConfirmed
                ? "Licence je platná a klient dostal krátkodobý bootstrap přístup pro bezpečné načtení konfigurace."
                : "Licence je platná a čeká na dokončení bezpečného propojení klienta se serverem.");
    }

    public async Task<LicenseStatusResponse> ConfirmAsync(LicenseConfirmationRequest request, CancellationToken cancellationToken)
    {
        ValidateRequiredText(request.ConfirmationCode, "Potvrzovací kód licence");
        ValidateRequiredText(request.ChallengeSignature, "Podpis challenge");

        var validation = await ValidateTokenCoreAsync(request.LicenseToken, request.InstallationId, request.MachineFingerprint, confirmLicense: true, cancellationToken);
        if (!string.Equals(validation.Binding.PendingConfirmationCode, request.ConfirmationCode.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Potvrzení licence nesouhlasí s posledním aktivačním krokem klienta.");
        }

        if (string.IsNullOrWhiteSpace(validation.Binding.PendingChallengeNonce) ||
            validation.Binding.PendingChallengeExpiresAtUtc is null ||
            validation.Binding.PendingChallengeExpiresAtUtc.Value <= DateTime.UtcNow)
        {
            throw new InvalidOperationException("Bezpečnostní challenge pro potvrzení licence už vypršel. Spusťte aktivaci znovu.");
        }

        ValidateChallengeSignature(validation.Binding, validation.ServerInstanceId, request);

        var state = NormalizeState(await licenseStore.LoadAsync(cancellationToken));
        var binding = state.Licenses.FirstOrDefault(item => item.LicenseId == validation.Binding.LicenseId)
            ?? throw new InvalidOperationException("Licence už na serveru neexistuje.");

        var confirmedBinding = binding with
        {
            IsConfirmed = true,
            ConfirmedAtUtc = DateTime.UtcNow,
            LastValidatedAtUtc = DateTime.UtcNow,
            PendingConfirmationCode = null,
            PendingChallengeNonce = null,
            PendingChallengeExpiresAtUtc = null
        };

        var licenses = state.Licenses.Select(item => item.LicenseId == confirmedBinding.LicenseId ? confirmedBinding : item).ToArray();
        var auditEntries = AppendAudit(
            state.AuditEntries,
            new LicenseAuditEntryRecord(
                Guid.NewGuid(),
                DateTime.UtcNow,
                confirmedBinding.LicenseId,
                "activation_confirmed",
                $"Licence pro {confirmedBinding.Email} byla potvrzená klientem po stažení konfigurace.",
                confirmedBinding.Email,
                confirmedBinding.InstallationId,
                true));

        await SaveStateAsync(state.ServerInstanceId, licenses, state.BootstrapSessions, state.BootstrapConfigurations, auditEntries, cancellationToken);
        var bootstrapSession = await IssueBootstrapSessionAsync(confirmedBinding, state.ServerInstanceId, cancellationToken);

        return CreateStatusResponse(
            confirmedBinding,
            state.ServerInstanceId,
            request.LicenseToken,
            bootstrapSession.SessionToken,
            bootstrapSession.Session.ExpiresAtUtc,
            "Active",
            "Licence byla plně potvrzená a klient může pokračovat do přihlášení.");
    }

    public async Task<ValidatedBootstrapContext> ValidateRequestAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var bootstrapToken = request.Headers["X-D3Bet-Bootstrap"].FirstOrDefault();
        var installationId = request.Headers["X-D3Bet-InstallationId"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(bootstrapToken) || string.IsNullOrWhiteSpace(installationId))
        {
            throw new InvalidOperationException("Klient neposlal platné bootstrap údaje.");
        }

        return await ValidateBootstrapSessionCoreAsync(bootstrapToken, installationId, requiredConfigurationId: null, cancellationToken);
    }

    public async Task<EncryptedClientConfigurationResponse> BuildEncryptedClientConfigurationAsync(
        ValidatedBootstrapContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var issuedAtUtc = DateTime.UtcNow;
        var expiresAtUtc = issuedAtUtc.AddMinutes(Math.Max(options.BootstrapConfigurationValidityMinutes, 5));
        var configVersion = GetBootstrapConfigurationVersion();
        var configId = $"CFG-{Guid.NewGuid():N}";

        var plainPayload = JsonSerializer.SerializeToUtf8Bytes(new OperatorClientConfigurationResponse(
            operatorOAuthOptions.Value.ClientId,
            operatorOAuthOptions.Value.RedirectUri,
            "openid profile roles offline_access operations",
            operatorOAuthOptions.Value.DisplayName));

        var nonce = RandomNumberGenerator.GetBytes(12);
        var key = DeriveConfigurationKey(context.BootstrapSessionToken, context.Binding.InstallationId, nonce);
        var cipher = new byte[plainPayload.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, plainPayload, cipher, tag);

        var state = NormalizeState(await licenseStore.LoadAsync(cancellationToken));
        var configurations = RevokeBootstrapConfigurations(
            state.BootstrapConfigurations,
            item => string.Equals(item.SessionId, context.Session.SessionId, StringComparison.Ordinal))
            .Append(new BootstrapConfigurationRecord(
                configId,
                context.Binding.LicenseId,
                context.Binding.InstallationId,
                context.Session.SessionId,
                issuedAtUtc,
                expiresAtUtc,
                false,
                configVersion))
            .ToArray();

        var sessions = (state.BootstrapSessions ?? [])
            .Select(item => item.SessionId == context.Session.SessionId
                ? item with { LastUsedAtUtc = DateTime.UtcNow }
                : item)
            .ToArray();

        var auditEntries = AppendAudit(
            state.AuditEntries,
            new LicenseAuditEntryRecord(
                Guid.NewGuid(),
                DateTime.UtcNow,
                context.Binding.LicenseId,
                "bootstrap_config_issued",
                $"Server vydal krátce žijící bootstrap konfiguraci {configId} pro {context.Binding.Email}.",
                context.Binding.Email,
                context.Binding.InstallationId,
                true));

        await SaveStateAsync(state.ServerInstanceId, state.Licenses, sessions, configurations, auditEntries, cancellationToken);

        var nonceBase64 = Convert.ToBase64String(nonce);
        var cipherBase64 = Convert.ToBase64String(cipher);
        var tagBase64 = Convert.ToBase64String(tag);
        var signature = CreateConfigurationSignature(configId, configVersion, issuedAtUtc, expiresAtUtc, nonceBase64, cipherBase64, tagBase64);

        return new EncryptedClientConfigurationResponse(
            configId,
            configVersion,
            issuedAtUtc,
            expiresAtUtc,
            nonceBase64,
            cipherBase64,
            tagBase64,
            "AES-256-GCM",
            $"v{configVersion}",
            signature);
    }

    public async Task ValidateBootstrapAuthorizationAsync(string bootstrapSessionToken, string configurationId, CancellationToken cancellationToken)
    {
        ValidateRequiredText(bootstrapSessionToken, "Bootstrap session token");
        ValidateRequiredText(configurationId, "Konfigurační ID");
        _ = await ValidateBootstrapSessionCoreAsync(bootstrapSessionToken.Trim(), installationId: null, configurationId.Trim(), cancellationToken);
    }

    private async Task<ValidatedLicenseContext> ValidateTokenCoreAsync(
        string licenseToken,
        string installationId,
        string machineFingerprint,
        bool confirmLicense,
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

        ValidateBinding(binding);

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

        var needsNewChallenge = !binding.IsConfirmed && NeedsNewChallenge(binding);
        var refreshedBinding = binding with
        {
            InstallationId = string.IsNullOrWhiteSpace(binding.InstallationId) ? normalizedInstallationId : binding.InstallationId,
            MachineFingerprintHash = string.IsNullOrWhiteSpace(binding.MachineFingerprintHash) ? normalizedFingerprintHash : binding.MachineFingerprintHash,
            LastValidatedAtUtc = DateTime.UtcNow,
            PendingChallengeNonce = needsNewChallenge ? Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)) : binding.PendingChallengeNonce,
            PendingChallengeExpiresAtUtc = needsNewChallenge ? DateTime.UtcNow.Add(PendingConfirmationWindow) : binding.PendingChallengeExpiresAtUtc
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
                confirmLicense && !binding.IsConfirmed ? "confirmation_requested" : "validated",
                confirmLicense && !binding.IsConfirmed
                    ? $"Klient připravil potvrzení licence pro {refreshedBinding.Email}."
                    : $"Licence pro {refreshedBinding.Email} byla ověřená klientem.",
                refreshedBinding.Email,
                refreshedBinding.InstallationId,
                true));
        await SaveStateAsync(state.ServerInstanceId, updatedLicenses, state.BootstrapSessions, state.BootstrapConfigurations, auditEntries, cancellationToken);

        return new ValidatedLicenseContext(refreshedBinding, state.ServerInstanceId, licenseToken);
    }

    private async Task<BootstrapSessionContext> IssueBootstrapSessionAsync(
        LicenseBindingRecord binding,
        string serverInstanceId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var expiresAtUtc = now.AddMinutes(Math.Max(options.BootstrapSessionValidityMinutes, 10));
        var sessionId = $"BSS-{Guid.NewGuid():N}";
        var session = new BootstrapSessionRecord(
            sessionId,
            string.Empty,
            binding.LicenseId,
            binding.InstallationId,
            now,
            expiresAtUtc,
            false,
            GetBootstrapConfigurationVersion());
        var token = CreateBootstrapSessionToken(session, serverInstanceId);
        session = session with { SessionTokenHash = ComputeSha256(token) };

        var state = NormalizeState(await licenseStore.LoadAsync(cancellationToken));
        var sessions = RevokeBootstrapSessions(
            state.BootstrapSessions,
            item => string.Equals(item.LicenseId, binding.LicenseId, StringComparison.Ordinal) &&
                string.Equals(item.InstallationId, binding.InstallationId, StringComparison.Ordinal))
            .Append(session)
            .ToArray();

        var configurations = RevokeBootstrapConfigurations(
            state.BootstrapConfigurations,
            item => string.Equals(item.LicenseId, binding.LicenseId, StringComparison.Ordinal) &&
                string.Equals(item.InstallationId, binding.InstallationId, StringComparison.Ordinal));

        var auditEntries = AppendAudit(
            state.AuditEntries,
            new LicenseAuditEntryRecord(
                Guid.NewGuid(),
                now,
                binding.LicenseId,
                "bootstrap_session_issued",
                $"Server vydal krátce žijící bootstrap session pro {binding.Email}.",
                binding.Email,
                binding.InstallationId,
                true));

        await SaveStateAsync(state.ServerInstanceId, state.Licenses, sessions, configurations, auditEntries, cancellationToken);
        return new BootstrapSessionContext(binding, session, token);
    }

    private async Task<ValidatedBootstrapContext> ValidateBootstrapSessionCoreAsync(
        string bootstrapSessionToken,
        string? installationId,
        string? requiredConfigurationId,
        CancellationToken cancellationToken)
    {
        var normalizedToken = bootstrapSessionToken.Trim();
        var parts = normalizedToken.Split('.', 2);
        if (parts.Length != 2)
        {
            throw new InvalidOperationException("Bootstrap session klienta má neplatný formát.");
        }

        var expectedSignature = ComputeHmac(parts[0]);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedSignature),
                Encoding.UTF8.GetBytes(parts[1])))
        {
            throw new InvalidOperationException("Bootstrap session token není podepsaný serverem.");
        }

        var payloadJson = Encoding.UTF8.GetString(Convert.FromBase64String(parts[0]));
        var payload = JsonSerializer.Deserialize<BootstrapSessionPayload>(payloadJson, SerializerOptions)
            ?? throw new InvalidOperationException("Bootstrap session token je poškozený.");

        var state = NormalizeState(await licenseStore.LoadAsync(cancellationToken));
        if (!string.Equals(payload.ServerInstanceId, state.ServerInstanceId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Bootstrap session nepatří k tomuto serveru.");
        }

        var binding = state.Licenses.FirstOrDefault(item => item.LicenseId == payload.LicenseId)
            ?? throw new InvalidOperationException("Licence pro bootstrap session už na serveru neexistuje.");
        ValidateBinding(binding);
        if (!binding.IsConfirmed)
        {
            throw new InvalidOperationException("Licence ještě nemá potvrzený handshake pro vydání bootstrap konfigurace.");
        }

        var sessionHash = ComputeSha256(normalizedToken);
        var session = (state.BootstrapSessions ?? []).FirstOrDefault(item =>
            string.Equals(item.SessionId, payload.SessionId, StringComparison.Ordinal) &&
            string.Equals(item.SessionTokenHash, sessionHash, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Bootstrap session už na serveru neexistuje.");

        if (session.IsRevoked)
        {
            throw new InvalidOperationException("Bootstrap session byla serverem zneplatněná.");
        }

        if (session.ExpiresAtUtc <= DateTime.UtcNow)
        {
            throw new InvalidOperationException("Krátkodobá bootstrap session vypršela. Načtěte si novou konfiguraci klienta.");
        }

        if (session.ConfigurationVersion != GetBootstrapConfigurationVersion())
        {
            throw new InvalidOperationException("Bootstrap session už neodpovídá aktuální verzi serverového bootstrapu.");
        }

        if (!string.Equals(session.LicenseId, binding.LicenseId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Bootstrap session nepatří k aktuální licenci.");
        }

        if (!string.IsNullOrWhiteSpace(installationId) &&
            !string.Equals(session.InstallationId, installationId.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Bootstrap session nepatří k této instalaci klienta.");
        }

        BootstrapConfigurationRecord? configuration = null;
        if (!string.IsNullOrWhiteSpace(requiredConfigurationId))
        {
            configuration = (state.BootstrapConfigurations ?? []).FirstOrDefault(item => string.Equals(item.ConfigId, requiredConfigurationId.Trim(), StringComparison.Ordinal))
                ?? throw new InvalidOperationException("Požadovaná bootstrap konfigurace už na serveru neexistuje.");

            if (configuration.IsRevoked)
            {
                throw new InvalidOperationException("Bootstrap konfigurace byla serverem zneplatněná.");
            }

            if (configuration.ExpiresAtUtc <= DateTime.UtcNow)
            {
                throw new InvalidOperationException("Bootstrap konfigurace už vypršela. Načtěte si prosím novou.");
            }

            if (configuration.ConfigurationVersion != GetBootstrapConfigurationVersion())
            {
                throw new InvalidOperationException("Bootstrap konfigurace už neodpovídá aktuální verzi serveru.");
            }

            if (!string.Equals(configuration.SessionId, session.SessionId, StringComparison.Ordinal) ||
                !string.Equals(configuration.LicenseId, binding.LicenseId, StringComparison.Ordinal) ||
                !string.Equals(configuration.InstallationId, session.InstallationId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Bootstrap konfigurace nepatří k této licenci nebo instalaci.");
            }
        }

        var updatedSessions = (state.BootstrapSessions ?? [])
            .Select(item => item.SessionId == session.SessionId
                ? item with { LastUsedAtUtc = DateTime.UtcNow }
                : item)
            .ToArray();
        var updatedConfigurations = configuration is null
            ? (state.BootstrapConfigurations ?? []).ToArray()
            : (state.BootstrapConfigurations ?? [])
                .Select(item => item.ConfigId == configuration.ConfigId
                    ? item with { LastUsedAtUtc = DateTime.UtcNow }
                    : item)
                .ToArray();

        await SaveStateAsync(state.ServerInstanceId, state.Licenses, updatedSessions, updatedConfigurations, state.AuditEntries, cancellationToken);
        return new ValidatedBootstrapContext(binding, session, normalizedToken, configuration);
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

    private string CreateBootstrapSessionToken(BootstrapSessionRecord session, string serverInstanceId)
    {
        var payload = new BootstrapSessionPayload(
            session.SessionId,
            session.LicenseId,
            serverInstanceId,
            session.InstallationId,
            session.ConfigurationVersion,
            session.IssuedAtUtc,
            session.ExpiresAtUtc);
        var payloadJson = JsonSerializer.Serialize(payload, SerializerOptions);
        var payloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson));
        var signature = ComputeHmac(payloadBase64);
        return $"{payloadBase64}.{signature}";
    }

    private LicenseStatusResponse CreateStatusResponse(
        LicenseBindingRecord binding,
        string serverInstanceId,
        string token,
        string? bootstrapSessionToken,
        DateTime? bootstrapSessionExpiresAtUtc,
        string status,
        string message)
    {
        return new LicenseStatusResponse(
            true,
            status,
            message,
            token,
            bootstrapSessionToken ?? string.Empty,
            bootstrapSessionExpiresAtUtc,
            binding.PendingConfirmationCode ?? string.Empty,
            binding.PendingChallengeNonce ?? string.Empty,
            binding.PendingChallengeExpiresAtUtc,
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

        ValidateClientPublicKey(request.ClientPublicKey);
    }

    private byte[] DeriveConfigurationKey(string bootstrapSessionToken, string installationId, byte[] nonce)
    {
        var material = $"{options.SharedSecret}|{bootstrapSessionToken}|{installationId}|{Convert.ToBase64String(nonce)}";
        return SHA256.HashData(Encoding.UTF8.GetBytes(material));
    }

    private string CreateConfigurationSignature(
        string configId,
        int configVersion,
        DateTime issuedAtUtc,
        DateTime expiresAtUtc,
        string nonce,
        string cipherText,
        string tag)
    {
        var payload = $"{configId}|{configVersion}|{issuedAtUtc:O}|{expiresAtUtc:O}|{nonce}|{cipherText}|{tag}";
        return ComputeHmac(payload);
    }

    private string ComputeHmac(string payloadBase64)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(options.SharedSecret));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadBase64)));
    }

    private static string ComputeSha256(string value) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private int GetBootstrapConfigurationVersion() =>
        Math.Max(options.BootstrapConfigurationVersion, 1);

    private static void ValidateRequiredText(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{label} je povinný.");
        }
    }

    private static bool NeedsNewChallenge(LicenseBindingRecord binding) =>
        string.IsNullOrWhiteSpace(binding.PendingChallengeNonce) ||
        binding.PendingChallengeExpiresAtUtc is null ||
        binding.PendingChallengeExpiresAtUtc.Value <= DateTime.UtcNow;

    private static void ValidateClientPublicKey(string clientPublicKey)
    {
        ValidateRequiredText(clientPublicKey, "Veřejný klíč klienta");
        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(clientPublicKey.Trim()), out _);
        }
        catch (Exception)
        {
            throw new InvalidOperationException("Klient poslal neplatný veřejný klíč pro licenční handshake.");
        }
    }

    private static void ValidateChallengeSignature(LicenseBindingRecord binding, string serverInstanceId, LicenseConfirmationRequest request)
    {
        if (string.IsNullOrWhiteSpace(binding.ClientPublicKey))
        {
            throw new InvalidOperationException("Licence nemá uložený veřejný klíč klienta pro potvrzení handshake.");
        }

        var payload = BuildChallengePayload(
            request.LicenseToken,
            binding.PendingConfirmationCode ?? string.Empty,
            binding.PendingChallengeNonce ?? string.Empty,
            request.InstallationId.Trim(),
            serverInstanceId);
        var data = Encoding.UTF8.GetBytes(payload);
        var signature = Convert.FromBase64String(request.ChallengeSignature.Trim());

        using var ecdsa = ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(binding.ClientPublicKey), out _);
        if (!ecdsa.VerifyData(data, signature, HashAlgorithmName.SHA256))
        {
            throw new InvalidOperationException("Podpis challenge neodpovídá registrované instalaci klienta.");
        }
    }

    private static string BuildChallengePayload(
        string licenseToken,
        string confirmationCode,
        string challengeNonce,
        string installationId,
        string serverInstanceId) =>
        $"{licenseToken}|{confirmationCode}|{challengeNonce}|{installationId}|{serverInstanceId}";

    private static void ValidateBinding(LicenseBindingRecord binding)
    {
        if (binding.IsRevoked)
        {
            throw new InvalidOperationException("Licence byla na serveru zablokovaná.");
        }

        if (binding.ExpiresAtUtc <= DateTime.UtcNow)
        {
            throw new InvalidOperationException("Licence už vypršela.");
        }
    }

    private static LicenseStoreState NormalizeState(LicenseStoreState state) =>
        state with
        {
            BootstrapSessions = state.BootstrapSessions ?? [],
            BootstrapConfigurations = state.BootstrapConfigurations ?? [],
            AuditEntries = state.AuditEntries ?? []
        };

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

    private static BootstrapSessionRecord[] RevokeBootstrapSessions(
        IReadOnlyList<BootstrapSessionRecord>? sessions,
        Func<BootstrapSessionRecord, bool> predicate)
    {
        return (sessions ?? [])
            .Select(item => predicate(item) ? item with { IsRevoked = true } : item)
            .ToArray();
    }

    private static BootstrapConfigurationRecord[] RevokeBootstrapConfigurations(
        IReadOnlyList<BootstrapConfigurationRecord>? configurations,
        Func<BootstrapConfigurationRecord, bool> predicate)
    {
        return (configurations ?? [])
            .Select(item => predicate(item) ? item with { IsRevoked = true } : item)
            .ToArray();
    }

    private Task SaveStateAsync(
        string serverInstanceId,
        IReadOnlyList<LicenseBindingRecord> licenses,
        IReadOnlyList<BootstrapSessionRecord>? sessions,
        IReadOnlyList<BootstrapConfigurationRecord>? configurations,
        IReadOnlyList<LicenseAuditEntryRecord>? auditEntries,
        CancellationToken cancellationToken)
    {
        return licenseStore.SaveAsync(
            new LicenseStoreState(
                serverInstanceId,
                licenses,
                sessions?.ToArray(),
                configurations?.ToArray(),
                auditEntries?.ToArray()),
            cancellationToken);
    }
}

public sealed record ValidatedLicenseContext(LicenseBindingRecord Binding, string ServerInstanceId, string LicenseToken);

public sealed record ValidatedBootstrapContext(
    LicenseBindingRecord Binding,
    BootstrapSessionRecord Session,
    string BootstrapSessionToken,
    BootstrapConfigurationRecord? Configuration);

internal sealed record LicenseTokenPayload(
    string LicenseId,
    string Email,
    string CustomerName,
    string ServerInstanceId,
    string InstallationId,
    string MachineFingerprintHash,
    DateTime IssuedAtUtc,
    DateTime ExpiresAtUtc);

internal sealed record BootstrapSessionPayload(
    string SessionId,
    string LicenseId,
    string ServerInstanceId,
    string InstallationId,
    int ConfigurationVersion,
    DateTime IssuedAtUtc,
    DateTime ExpiresAtUtc);

internal sealed record BootstrapSessionContext(
    LicenseBindingRecord Binding,
    BootstrapSessionRecord Session,
    string SessionToken);
