using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BettingApp.Server.Data;

public sealed class ServerIdentityDbContext(DbContextOptions<ServerIdentityDbContext> options)
    : IdentityDbContext<IdentityUser, IdentityRole, string>(options)
{
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.UseOpenIddict();

        builder.Entity<AuditLogEntry>(entity =>
        {
            entity.ToTable("AuditLogEntries");
            entity.HasKey(entry => entry.Id);
            entity.Property(entry => entry.Action).HasMaxLength(120).IsRequired();
            entity.Property(entry => entry.EntityType).HasMaxLength(120).IsRequired();
            entity.Property(entry => entry.EntityId).HasMaxLength(200).IsRequired();
            entity.Property(entry => entry.ActorId).HasMaxLength(200).IsRequired();
            entity.Property(entry => entry.ActorName).HasMaxLength(200).IsRequired();
            entity.Property(entry => entry.ActorRoles).HasMaxLength(300).IsRequired();
            entity.Property(entry => entry.TraceId).HasMaxLength(100).IsRequired();
            entity.HasIndex(entry => entry.CreatedAtUtc);
            entity.HasIndex(entry => new { entry.EntityType, entry.EntityId });
        });
    }
}
