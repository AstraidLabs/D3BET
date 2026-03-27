namespace BettingApp.Server.Models;

public sealed record AdminUserListResponse(
    int Page,
    int PageSize,
    int TotalCount,
    string[] AvailableRoles,
    AdminUserListItemResponse[] Items);

public sealed record AdminUserListItemResponse(
    string Id,
    string UserName,
    string? Email,
    bool EmailConfirmed,
    bool IsBlocked,
    string[] Roles,
    Guid? BettorId,
    decimal CreditBalance,
    string CreditCode,
    int BetCount,
    DateTime? LastBetPlacedAtUtc);
