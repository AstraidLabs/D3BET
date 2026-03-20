namespace BettingApp.Server.Models;

public sealed record AuditLogEntryResponse(
    long Id,
    DateTime CreatedAtUtc,
    string Action,
    string EntityType,
    string EntityId,
    string ActorId,
    string ActorName,
    string ActorRoles,
    string TraceId,
    string? DetailJson);
