namespace BettingApp.Server.Models;

public sealed record OperatorProfileResponse(
    string UserId,
    string UserName,
    IReadOnlyList<string> Roles);
