using BettingApp.Server.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BettingApp.Server.Data;

public sealed class ServerIdentityDbContextFactory : IDesignTimeDbContextFactory<ServerIdentityDbContext>
{
    public ServerIdentityDbContext CreateDbContext(string[] args)
    {
        var authDatabasePath = ServerStoragePaths.GetAuthDatabasePath();
        Directory.CreateDirectory(Path.GetDirectoryName(authDatabasePath)!);

        var optionsBuilder = new DbContextOptionsBuilder<ServerIdentityDbContext>();
        optionsBuilder.UseSqlite($"Data Source={authDatabasePath}");

        return new ServerIdentityDbContext(optionsBuilder.Options);
    }
}
