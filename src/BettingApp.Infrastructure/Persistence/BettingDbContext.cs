using BettingApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BettingApp.Infrastructure.Persistence;

public sealed class BettingDbContext(DbContextOptions<BettingDbContext> options) : DbContext(options)
{
    public DbSet<Bettor> Bettors => Set<Bettor>();

    public DbSet<BettingMarket> BettingMarkets => Set<BettingMarket>();

    public DbSet<Bet> Bets => Set<Bet>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Bettor>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<Bet>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Odds).HasPrecision(10, 2);
            entity.Property(x => x.Stake).HasPrecision(10, 2);
            entity.Property(x => x.IsWinning).HasDefaultValue(false);
            entity.Property(x => x.OutcomeStatus).HasDefaultValue(BetOutcomeStatus.Pending);
            entity.Property(x => x.IsCommissionFeePaid).HasDefaultValue(false);
            entity.HasOne(x => x.BettingMarket)
                .WithMany(x => x.Bets)
                .HasForeignKey(x => x.BettingMarketId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Bettor)
                .WithMany(x => x.Bets)
                .HasForeignKey(x => x.BettorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BettingMarket>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.OpeningOdds).HasPrecision(10, 2);
            entity.Property(x => x.CurrentOdds).HasPrecision(10, 2);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
        });
    }
}
