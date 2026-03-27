using BettingApp.Server.Models;

namespace BettingApp.Server.Services;

public sealed class AdminLicenseManagementService(LicenseStore licenseStore)
{
    private const int ExpiringSoonDays = 21;

    public async Task<LicenseAdminOverviewResponse> GetOverviewAsync(int auditLimit, CancellationToken cancellationToken)
    {
        var state = await licenseStore.LoadAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var licenses = state.Licenses
            .OrderBy(item => item.Email, StringComparer.OrdinalIgnoreCase)
            .Select(item =>
            {
                var isExpiringSoon = !item.IsRevoked && item.ExpiresAtUtc <= now.AddDays(ExpiringSoonDays);
                var statusLabel = item.IsRevoked
                    ? "Zablokovaná"
                    : !item.IsConfirmed
                        ? "Čeká na potvrzení"
                    : isExpiringSoon
                        ? "Brzy vyprší"
                        : "Aktivní";

                return new LicenseAdminItemResponse(
                    item.LicenseId,
                    item.Email,
                    item.CustomerName,
                    item.InstallationId,
                    item.IsRevoked,
                    item.IsConfirmed,
                    isExpiringSoon,
                    item.IssuedAtUtc,
                    item.ExpiresAtUtc,
                    item.LastValidatedAtUtc,
                    statusLabel);
            })
            .ToArray();

        var auditEntries = (state.AuditEntries ?? [])
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(Math.Max(auditLimit, 10))
            .Select(item => new LicenseAuditEntryResponse(
                item.Id,
                item.CreatedAtUtc,
                item.LicenseId,
                item.EventType,
                item.DisplayMessage,
                item.Email,
                item.InstallationId,
                item.IsSuccessful))
            .ToArray();

        return new LicenseAdminOverviewResponse(
            state.ServerInstanceId,
            licenses.Length,
            licenses.Count(item => !item.IsRevoked && item.IsConfirmed),
            licenses.Count(item => !item.IsRevoked && !item.IsConfirmed),
            licenses.Count(item => item.IsRevoked),
            licenses.Count(item => item.IsExpiringSoon),
            licenses,
            auditEntries);
    }

    public Task<LicenseAdminOverviewResponse> RevokeAsync(string licenseId, string? reason, CancellationToken cancellationToken) =>
        UpdateLicenseAsync(
            licenseId,
            "revoked",
            item => item with { IsRevoked = true },
            item => $"Licence pro {item.Email} byla zablokovaná.{FormatReason(reason)}",
            cancellationToken);

    public Task<LicenseAdminOverviewResponse> RestoreAsync(string licenseId, string? reason, CancellationToken cancellationToken) =>
        UpdateLicenseAsync(
            licenseId,
            "restored",
            item => item with { IsRevoked = false },
            item => $"Licence pro {item.Email} byla znovu povolená.{FormatReason(reason)}",
            cancellationToken);

    public Task<LicenseAdminOverviewResponse> ReleaseAsync(string licenseId, string? reason, CancellationToken cancellationToken) =>
        UpdateLicenseAsync(
            licenseId,
            "released",
            item => item with
            {
                InstallationId = string.Empty,
                MachineFingerprintHash = string.Empty,
                LastValidatedAtUtc = null,
                IsConfirmed = false,
                ConfirmedAtUtc = null,
                ClientPublicKey = null,
                PendingConfirmationCode = null,
                PendingActivatedAtUtc = null,
                PendingChallengeNonce = null,
                PendingChallengeExpiresAtUtc = null
            },
            item => $"Licence pro {item.Email} byla uvolněná pro nové zařízení.{FormatReason(reason)}",
            cancellationToken);

    public Task<LicenseAdminOverviewResponse> ExtendAsync(string licenseId, int additionalDays, string? reason, CancellationToken cancellationToken)
    {
        if (additionalDays <= 0)
        {
            throw new InvalidOperationException("Prodloužení licence musí být alespoň o 1 den.");
        }

        return UpdateLicenseAsync(
            licenseId,
            "extended",
            item => item with { ExpiresAtUtc = item.ExpiresAtUtc.AddDays(additionalDays) },
            item => $"Licence pro {item.Email} byla prodloužená o {additionalDays} dní.{FormatReason(reason)}",
            cancellationToken);
    }

    private async Task<LicenseAdminOverviewResponse> UpdateLicenseAsync(
        string licenseId,
        string eventType,
        Func<LicenseBindingRecord, LicenseBindingRecord> update,
        Func<LicenseBindingRecord, string> messageFactory,
        CancellationToken cancellationToken)
    {
        var state = await licenseStore.LoadAsync(cancellationToken);
        var target = state.Licenses.FirstOrDefault(item => string.Equals(item.LicenseId, licenseId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Požadovaná licence na serveru neexistuje.");

        var updated = update(target);
        var licenses = state.Licenses
            .Select(item => string.Equals(item.LicenseId, licenseId, StringComparison.Ordinal) ? updated : item)
            .ToArray();
        var bootstrapSessions = (state.BootstrapSessions ?? [])
            .Select(item => string.Equals(item.LicenseId, licenseId, StringComparison.Ordinal)
                ? item with { IsRevoked = true }
                : item)
            .ToArray();
        var bootstrapConfigurations = (state.BootstrapConfigurations ?? [])
            .Select(item => string.Equals(item.LicenseId, licenseId, StringComparison.Ordinal)
                ? item with { IsRevoked = true }
                : item)
            .ToArray();

        var auditEntries = AppendAudit(
            state.AuditEntries,
            new LicenseAuditEntryRecord(
                Guid.NewGuid(),
                DateTime.UtcNow,
                updated.LicenseId,
                eventType,
                messageFactory(updated),
                updated.Email,
                updated.InstallationId,
                true));

        await licenseStore.SaveAsync(new LicenseStoreState(state.ServerInstanceId, licenses, bootstrapSessions, bootstrapConfigurations, auditEntries), cancellationToken);
        return await GetOverviewAsync(40, cancellationToken);
    }

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

    private static string FormatReason(string? reason)
    {
        return string.IsNullOrWhiteSpace(reason)
            ? string.Empty
            : $" Důvod: {reason.Trim()}";
    }
}
