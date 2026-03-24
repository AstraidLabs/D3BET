using BettingApp.Infrastructure.Persistence;
using BettingApp.Server.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BettingApp.Server.Data;

public sealed class BettingDbContextFactory : IDesignTimeDbContextFactory<BettingDbContext>
{
    public BettingDbContext CreateDbContext(string[] args)
    {
        var bettingDatabasePath = ServerStoragePaths.GetBettingDatabasePath();
        Directory.CreateDirectory(Path.GetDirectoryName(bettingDatabasePath)!);

        var optionsBuilder = new DbContextOptionsBuilder<BettingDbContext>();
        optionsBuilder.UseSqlite($"Data Source={bettingDatabasePath}");

        return new BettingDbContext(optionsBuilder.Options);
    }
}
