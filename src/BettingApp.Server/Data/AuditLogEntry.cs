namespace BettingApp.Server.Data;

public sealed class AuditLogEntry
{
    public long Id { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string ActorId { get; set; } = string.Empty;

    public string ActorName { get; set; } = string.Empty;

    public string ActorRoles { get; set; } = string.Empty;

    public string TraceId { get; set; } = string.Empty;

    public string? DetailJson { get; set; }
}
