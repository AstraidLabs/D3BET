namespace BettingApp.Server.Models;

public sealed record LicenseAdminOverviewResponse(
    string ServerInstanceId,
    int TotalLicenses,
    int ActiveLicenses,
    int RevokedLicenses,
    int ExpiringSoonLicenses,
    IReadOnlyList<LicenseAdminItemResponse> Licenses,
    IReadOnlyList<LicenseAuditEntryResponse> AuditEntries);

public sealed record LicenseAdminItemResponse(
    string LicenseId,
    string Email,
    string CustomerName,
    string InstallationId,
    bool IsRevoked,
    bool IsExpiringSoon,
    DateTime IssuedAtUtc,
    DateTime ExpiresAtUtc,
    DateTime? LastValidatedAtUtc,
    string StatusLabel);

public sealed record LicenseAuditEntryResponse(
    Guid Id,
    DateTime CreatedAtUtc,
    string LicenseId,
    string EventType,
    string DisplayMessage,
    string Email,
    string InstallationId,
    bool IsSuccessful);
