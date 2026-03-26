using BettingApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BettingApp.Infrastructure.Persistence;

public sealed class BettingDbContext(DbContextOptions<BettingDbContext> options) : DbContext(options)
{
    public DbSet<Bettor> Bettors => Set<Bettor>();

    public DbSet<BettingMarket> BettingMarkets => Set<BettingMarket>();

    public DbSet<Bet> Bets => Set<Bet>();

    public DbSet<BettorWallet> BettorWallets => Set<BettorWallet>();

    public DbSet<D3CreditTransaction> D3CreditTransactions => Set<D3CreditTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Bettor>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
            entity.HasOne(x => x.Wallet)
                .WithOne(x => x.Bettor)
                .HasForeignKey<BettorWallet>(x => x.BettorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Bet>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Odds).HasPrecision(10, 2);
            entity.Property(x => x.Stake).HasPrecision(10, 2);
            entity.Property(x => x.StakeCurrencyCode).HasMaxLength(16).IsRequired();
            entity.Property(x => x.StakeRealMoneyEquivalent).HasPrecision(10, 2);
            entity.Property(x => x.CreditToMoneyRateApplied).HasPrecision(12, 4);
            entity.Property(x => x.MarketParticipationMultiplierApplied).HasPrecision(12, 4);
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

        modelBuilder.Entity<BettorWallet>(entity =>
        {
            entity.HasKey(x => x.BettorId);
            entity.Property(x => x.Balance).HasPrecision(12, 2);
            entity.Property(x => x.CreditCode).HasMaxLength(32).IsRequired();
            entity.Property(x => x.LastMoneyToCreditRate).HasPrecision(12, 4);
            entity.Property(x => x.LastCreditToMoneyRate).HasPrecision(12, 4);
        });

        modelBuilder.Entity<D3CreditTransaction>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CreditAmount).HasPrecision(12, 2);
            entity.Property(x => x.RealMoneyAmount).HasPrecision(12, 2);
            entity.Property(x => x.RealCurrencyCode).HasMaxLength(16).IsRequired();
            entity.Property(x => x.MoneyToCreditRate).HasPrecision(12, 4);
            entity.Property(x => x.CreditToMoneyRate).HasPrecision(12, 4);
            entity.Property(x => x.MarketParticipationMultiplier).HasPrecision(12, 4);
            entity.Property(x => x.Reference).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(240).IsRequired();
            entity.HasOne(x => x.Bettor)
                .WithMany(x => x.CreditTransactions)
                .HasForeignKey(x => x.BettorId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.BettorId, x.CreatedAtUtc });
        });
    }
}
