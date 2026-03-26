namespace BettingApp.Server.Models;

public sealed record AccountProfileResponse(
    string UserId,
    string UserName,
    string? Email,
    bool EmailConfirmed,
    IReadOnlyList<string> Roles);
