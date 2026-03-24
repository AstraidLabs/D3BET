using BettingApp.Infrastructure.Persistence;
using BettingApp.Server.Configuration;
using BettingApp.Server.Data;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using System.Data;

namespace BettingApp.Server.Services;

public sealed class ServerBootstrapHostedService(
    IServiceProvider serviceProvider,
    IOptions<KioskOAuthClientOptions> kioskOptions,
    IOptions<OperatorOAuthClientOptions> operatorOptions,
    IOptions<BootstrapIdentityOptions> bootstrapIdentityOptions,
    ILogger<ServerBootstrapHostedService> logger) : IHostedService
{
    private const string InitialIdentityMigrationId = "20260320120807_InitialIdentityOpenIddict";
    private const string EfProductVersion = "10.0.5";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();

        var bettingInitialization = scope.ServiceProvider.GetRequiredService<DatabaseInitializationService>();
        await bettingInitialization.InitializeAsync(cancellationToken);

        var identityDbContext = scope.ServiceProvider.GetRequiredService<ServerIdentityDbContext>();
        await PrepareLegacyIdentityDatabaseAsync(identityDbContext, cancellationToken);
        await identityDbContext.Database.MigrateAsync(cancellationToken);

        await SeedRolesAsync(scope.ServiceProvider, cancellationToken);
        await SeedBootstrapUsersAsync(scope.ServiceProvider, cancellationToken);
        await SeedKioskClientAsync(scope.ServiceProvider, cancellationToken);
        await SeedOperatorClientAsync(scope.ServiceProvider, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedRolesAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var roleName in new[] { Roles.Admin, Roles.Operator })
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new IdentityRole(roleName));
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Nepodarilo se vytvorit roli '{roleName}'.");
            }
        }
    }

    private async Task SeedKioskClientAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var applicationManager = services.GetRequiredService<IOpenIddictApplicationManager>();
        var options = kioskOptions.Value;

        var existingApplication = await applicationManager.FindByClientIdAsync(options.ClientId, cancellationToken);
        if (existingApplication is not null)
        {
            return;
        }

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = options.ClientId,
            ClientSecret = options.ClientSecret,
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            DisplayName = options.DisplayName,
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                OpenIddictConstants.Permissions.Prefixes.Scope + Configuration.Scopes.DisplayRead
            }
        };

        await applicationManager.CreateAsync(descriptor, cancellationToken);
        logger.LogInformation("OAuth klient pro kiosk '{ClientId}' byl vytvoren.", options.ClientId);
    }

    private async Task SeedOperatorClientAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var applicationManager = services.GetRequiredService<IOpenIddictApplicationManager>();
        var options = operatorOptions.Value;

        var existingApplication = await applicationManager.FindByClientIdAsync(options.ClientId, cancellationToken);
        if (existingApplication is not null)
        {
            await applicationManager.DeleteAsync(existingApplication, cancellationToken);
            logger.LogInformation("Existující OAuth klient '{ClientId}' byl odstraněn před opětovným vytvořením.", options.ClientId);
        }

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = options.ClientId,
            ClientType = OpenIddictConstants.ClientTypes.Public,
            ConsentType = OpenIddictConstants.ConsentTypes.Explicit,
            DisplayName = options.DisplayName,
            RedirectUris =
            {
                new Uri(options.RedirectUri)
            },
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                OpenIddictConstants.Permissions.GrantTypes.Password,
                OpenIddictConstants.Permissions.ResponseTypes.Code,
                OpenIddictConstants.Permissions.Prefixes.Scope + Configuration.Scopes.OpenId,
                OpenIddictConstants.Permissions.Prefixes.Scope + Configuration.Scopes.Profile,
                OpenIddictConstants.Permissions.Prefixes.Scope + Configuration.Scopes.Roles,
                OpenIddictConstants.Permissions.Prefixes.Scope + Configuration.Scopes.OfflineAccess,
                OpenIddictConstants.Permissions.Prefixes.Scope + Configuration.Scopes.Operations
            },
            Requirements =
            {
                OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange
            }
        };

        await applicationManager.CreateAsync(descriptor, cancellationToken);
        logger.LogInformation("OAuth klient pro provozovatele '{ClientId}' byl vytvoren.", options.ClientId);
    }

    private async Task SeedBootstrapUsersAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var options = bootstrapIdentityOptions.Value;

        await EnsureUserInRoleAsync(userManager, options.Admin, Roles.Admin, cancellationToken);
        await EnsureUserInRoleAsync(userManager, options.Operator, Roles.Operator, cancellationToken);
    }

    private async Task EnsureUserInRoleAsync(
        UserManager<IdentityUser> userManager,
        BootstrapUserOptions userOptions,
        string roleName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(userOptions.UserName) || string.IsNullOrWhiteSpace(userOptions.Password))
        {
            logger.LogWarning("Bootstrap uzivatel pro roli '{RoleName}' nema kompletni konfiguraci, seed se preskakuje.", roleName);
            return;
        }

        var user = await userManager.FindByNameAsync(userOptions.UserName);
        if (user is null)
        {
            user = new IdentityUser
            {
                UserName = userOptions.UserName
            };

            var createResult = await userManager.CreateAsync(user, userOptions.Password);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Nepodarilo se vytvorit bootstrap uzivatele '{userOptions.UserName}': {string.Join(", ", createResult.Errors.Select(error => error.Description))}");
            }

            logger.LogInformation("Bootstrap uzivatel '{UserName}' byl vytvoren.", userOptions.UserName);
        }

        if (await userManager.IsInRoleAsync(user, roleName))
        {
            return;
        }

        var addToRoleResult = await userManager.AddToRoleAsync(user, roleName);
        if (!addToRoleResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Nepodarilo se priradit uzivateli '{userOptions.UserName}' roli '{roleName}': {string.Join(", ", addToRoleResult.Errors.Select(error => error.Description))}");
        }
    }

    private static async Task PrepareLegacyIdentityDatabaseAsync(
        ServerIdentityDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await using var connection = (SqliteConnection)dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var hasMigrationHistoryTable = await HasMigrationHistoryTableAsync(connection, cancellationToken);
        var hasLegacyIdentityTables = await HasLegacyIdentityTablesAsync(connection, cancellationToken);

        if (!hasMigrationHistoryTable && !hasLegacyIdentityTables)
        {
            return;
        }

        if (!hasMigrationHistoryTable)
        {
            await EnsureMigrationHistoryTableAsync(connection, cancellationToken);
        }

        if (hasLegacyIdentityTables)
        {
            await EnsureInitialMigrationHistoryRowAsync(connection, cancellationToken);
        }
    }

    private static async Task<bool> HasMigrationHistoryTableAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table' AND name = '__EFMigrationsHistory';
            """;

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result) > 0;
    }

    private static async Task<bool> HasLegacyIdentityTablesAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        string[] expectedTables =
        [
            "AspNetUsers",
            "AspNetRoles",
            "OpenIddictApplications"
        ];

        foreach (var tableName in expectedTables)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*)
                FROM sqlite_master
                WHERE type = 'table' AND name = $tableName;
                """;
            command.Parameters.AddWithValue("$tableName", tableName);

            var exists = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
            if (!exists)
            {
                return false;
            }
        }

        return true;
    }

    private static async Task EnsureMigrationHistoryTableAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                "ProductVersion" TEXT NOT NULL
            );
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureInitialMigrationHistoryRowAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var existsCommand = connection.CreateCommand();
        existsCommand.CommandText = """
            SELECT COUNT(*)
            FROM "__EFMigrationsHistory"
            WHERE "MigrationId" = $migrationId;
            """;
        existsCommand.Parameters.AddWithValue("$migrationId", InitialIdentityMigrationId);

        var exists = Convert.ToInt32(await existsCommand.ExecuteScalarAsync(cancellationToken)) > 0;
        if (exists)
        {
            return;
        }

        await using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText = """
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            VALUES ($migrationId, $productVersion);
            """;
        insertCommand.Parameters.AddWithValue("$migrationId", InitialIdentityMigrationId);
        insertCommand.Parameters.AddWithValue("$productVersion", EfProductVersion);
        await insertCommand.ExecuteNonQueryAsync(cancellationToken);
    }
}
