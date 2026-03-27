namespace BettingApp.Server.Models;

public sealed record LicenseExtendRequest(int AdditionalDays, string? Reason);
