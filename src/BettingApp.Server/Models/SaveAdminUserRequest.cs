namespace BettingApp.Server.Models;

public sealed record SaveAdminUserRequest(
    string UserName,
    string Email,
    bool EmailConfirmed,
    string[] Roles,
    string? Password);
