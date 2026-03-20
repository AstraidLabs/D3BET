using OpenIddict.Abstractions;
using System.Text.Json;
using BettingApp.Server.Data;
using BettingApp.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace BettingApp.Server.Services;

public sealed class AuditLogService(
    ILogger<AuditLogService> logger,
    ServerIdentityDbContext dbContext)
{
    public async Task RecordAsync(HttpContext context, string action, string entityType, string entityId, object? detail = null, CancellationToken cancellationToken = default)
    {
        var actorId = context.User.GetClaim(OpenIddictConstants.Claims.Subject)
            ?? context.User.GetClaim(OpenIddictConstants.Claims.ClientId)
            ?? context.User.Identity?.Name
            ?? "anonymous";

        var actorName = context.User.GetClaim(OpenIddictConstants.Claims.Name)
            ?? context.User.Identity?.Name
            ?? actorId;

        var actorRoles = context.User.Claims
            .Where(claim => claim.Type == OpenIddictConstants.Claims.Role)
            .Select(claim => claim.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var entry = new AuditLogEntry
        {
            CreatedAtUtc = DateTime.UtcNow,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            ActorId = actorId,
            ActorName = actorName,
            ActorRoles = string.Join(", ", actorRoles),
            TraceId = context.TraceIdentifier,
            DetailJson = detail is null ? null : JsonSerializer.Serialize(detail)
        };

        dbContext.AuditLogEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "AUDIT {Action} {EntityType} {EntityId} actorId={ActorId} actorName={ActorName} roles={Roles} traceId={TraceId} detail={@Detail}",
            action,
            entityType,
            entityId,
            actorId,
            actorName,
            actorRoles,
            context.TraceIdentifier,
            detail);
    }

    public async Task<IReadOnlyList<AuditLogEntryResponse>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
    {
        var sanitizedLimit = Math.Clamp(limit, 1, 200);

        return await dbContext.AuditLogEntries
            .AsNoTracking()
            .OrderByDescending(entry => entry.CreatedAtUtc)
            .Take(sanitizedLimit)
            .Select(entry => new AuditLogEntryResponse(
                entry.Id,
                entry.CreatedAtUtc,
                entry.Action,
                entry.EntityType,
                entry.EntityId,
                entry.ActorId,
                entry.ActorName,
                entry.ActorRoles,
                entry.TraceId,
                entry.DetailJson))
            .ToListAsync(cancellationToken);
    }
}
