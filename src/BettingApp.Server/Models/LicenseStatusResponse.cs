namespace BettingApp.Server.Models;

public sealed record LicenseStatusResponse(
    bool IsValid,
    string Status,
    string Message,
    string LicenseToken,
    string Email,
    string CustomerName,
    string ServerInstanceId,
    string InstallationId,
    DateTime IssuedAtUtc,
    DateTime ExpiresAtUtc);
