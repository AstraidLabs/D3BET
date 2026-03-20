using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace BettingApp.Infrastructure.Persistence;

public sealed class DatabaseInitializationService(BettingDbContext dbContext)
{
    private const string InitialMigrationId = "20260320000100_InitialCreate";
    private const string EfProductVersion = "10.0.5";

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await PrepareLegacyDatabaseAsync(cancellationToken);
        await dbContext.Database.MigrateAsync(cancellationToken);
        await EnsureSchemaCompatibilityAsync(cancellationToken);
        await EnableWalAsync(cancellationToken);
    }

    private async Task PrepareLegacyDatabaseAsync(CancellationToken cancellationToken)
    {
        await using var connection = (SqliteConnection)dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        if (await HasMigrationHistoryTableAsync(connection, cancellationToken))
        {
            return;
        }

        if (!await HasLegacyTablesAsync(connection, cancellationToken))
        {
            return;
        }

        await EnsureWinningColumnAsync(connection, cancellationToken);
        await EnsureCommissionFeePaidColumnAsync(connection, cancellationToken);
        await EnsureMigrationHistoryTableAsync(connection, cancellationToken);
        await EnsureInitialMigrationHistoryRowAsync(connection, cancellationToken);
    }

    private async Task EnsureSchemaCompatibilityAsync(CancellationToken cancellationToken)
    {
        await using var connection = (SqliteConnection)dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await EnsureBettingMarketsTableAsync(connection, cancellationToken);
        await EnsureWinningColumnAsync(connection, cancellationToken);
        await EnsureOutcomeStatusColumnAsync(connection, cancellationToken);
        await EnsureCommissionFeePaidColumnAsync(connection, cancellationToken);
        await EnsureBettingMarketIdColumnAsync(connection, cancellationToken);
        await BackfillBettingMarketsAsync(connection, cancellationToken);
    }

    private async Task EnableWalAsync(CancellationToken cancellationToken)
    {
        await using var connection = (SqliteConnection)dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL;";
        var result = await command.ExecuteScalarAsync(cancellationToken);

        if (!string.Equals(Convert.ToString(result), "wal", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Nepodarilo se zapnout SQLite WAL rezim.");
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

    private static async Task<bool> HasLegacyTablesAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table'
              AND name NOT LIKE 'sqlite_%'
              AND name <> '__EFMigrationsHistory';
            """;

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result) > 0;
    }

    private static async Task EnsureWinningColumnAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        if (!await HasTableAsync(connection, "Bets", cancellationToken))
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info('Bets');";

        var hasIsWinningColumn = false;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader.GetString(1), "IsWinning", StringComparison.OrdinalIgnoreCase))
            {
                hasIsWinningColumn = true;
                break;
            }
        }

        if (hasIsWinningColumn)
        {
            return;
        }

        await using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = "ALTER TABLE Bets ADD COLUMN IsWinning INTEGER NOT NULL DEFAULT 0;";
        await alterCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureCommissionFeePaidColumnAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        if (!await HasTableAsync(connection, "Bets", cancellationToken))
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info('Bets');";

        var hasCommissionFeePaidColumn = false;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader.GetString(1), "IsCommissionFeePaid", StringComparison.OrdinalIgnoreCase))
            {
                hasCommissionFeePaidColumn = true;
                break;
            }
        }

        if (hasCommissionFeePaidColumn)
        {
            return;
        }

        await using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = "ALTER TABLE Bets ADD COLUMN IsCommissionFeePaid INTEGER NOT NULL DEFAULT 0;";
        await alterCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureOutcomeStatusColumnAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        if (!await HasTableAsync(connection, "Bets", cancellationToken))
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info('Bets');";

        var hasOutcomeStatusColumn = false;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader.GetString(1), "OutcomeStatus", StringComparison.OrdinalIgnoreCase))
            {
                hasOutcomeStatusColumn = true;
                break;
            }
        }

        if (!hasOutcomeStatusColumn)
        {
            await using var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = "ALTER TABLE Bets ADD COLUMN OutcomeStatus INTEGER NOT NULL DEFAULT 0;";
            await alterCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var syncCommand = connection.CreateCommand();
        syncCommand.CommandText = """
            UPDATE "Bets"
            SET "OutcomeStatus" = CASE
                WHEN "IsWinning" = 1 THEN 1
                WHEN "OutcomeStatus" NOT IN (0, 1, 2) THEN 0
                ELSE "OutcomeStatus"
            END;
            """;
        await syncCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureBettingMarketsTableAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS "BettingMarkets" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_BettingMarkets" PRIMARY KEY,
                "EventName" TEXT NOT NULL,
                "OpeningOdds" TEXT NOT NULL,
                "CurrentOdds" TEXT NOT NULL,
                "IsActive" INTEGER NOT NULL DEFAULT 1,
                "CreatedAtUtc" TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureBettingMarketIdColumnAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        if (!await HasTableAsync(connection, "Bets", cancellationToken))
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info('Bets');";

        var hasBettingMarketIdColumn = false;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader.GetString(1), "BettingMarketId", StringComparison.OrdinalIgnoreCase))
            {
                hasBettingMarketIdColumn = true;
                break;
            }
        }

        if (hasBettingMarketIdColumn)
        {
            return;
        }

        await using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = "ALTER TABLE Bets ADD COLUMN BettingMarketId TEXT NULL;";
        await alterCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task BackfillBettingMarketsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        if (!await HasTableAsync(connection, "Bets", cancellationToken))
        {
            return;
        }

        var legacyGroups = new List<(string EventName, decimal Odds, DateTime CreatedAtUtc)>();

        await using (var selectCommand = connection.CreateCommand())
        {
            selectCommand.CommandText = """
                SELECT b."EventName", b."Odds", MAX(b."PlacedAtUtc")
                FROM "Bets" b
                LEFT JOIN "BettingMarkets" m ON m."Id" = b."BettingMarketId"
                WHERE b."BettingMarketId" IS NULL AND b."EventName" IS NOT NULL AND TRIM(b."EventName") <> ''
                GROUP BY b."EventName", b."Odds";
                """;

            await using var reader = await selectCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var eventName = reader.GetString(0);
                var odds = reader.GetDecimal(1);
                var createdAtUtc = reader.GetDateTime(2);
                legacyGroups.Add((eventName, odds, createdAtUtc));
            }
        }

        foreach (var group in legacyGroups)
        {
            var marketId = Guid.NewGuid().ToString();

            await using (var insertMarketCommand = connection.CreateCommand())
            {
                insertMarketCommand.CommandText = """
                    INSERT INTO "BettingMarkets" ("Id", "EventName", "OpeningOdds", "CurrentOdds", "IsActive", "CreatedAtUtc")
                    VALUES ($id, $eventName, $openingOdds, $currentOdds, 1, $createdAtUtc);
                    """;
                insertMarketCommand.Parameters.AddWithValue("$id", marketId);
                insertMarketCommand.Parameters.AddWithValue("$eventName", group.EventName);
                insertMarketCommand.Parameters.AddWithValue("$openingOdds", group.Odds);
                insertMarketCommand.Parameters.AddWithValue("$currentOdds", group.Odds);
                insertMarketCommand.Parameters.AddWithValue("$createdAtUtc", group.CreatedAtUtc);
                await insertMarketCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await using var updateBetCommand = connection.CreateCommand();
            updateBetCommand.CommandText = """
                UPDATE "Bets"
                SET "BettingMarketId" = $marketId
                WHERE "BettingMarketId" IS NULL AND "EventName" = $eventName AND "Odds" = $odds;
                """;
            updateBetCommand.Parameters.AddWithValue("$marketId", marketId);
            updateBetCommand.Parameters.AddWithValue("$eventName", group.EventName);
            updateBetCommand.Parameters.AddWithValue("$odds", group.Odds);
            await updateBetCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<bool> HasTableAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table' AND name = $tableName;
            """;
        command.Parameters.AddWithValue("$tableName", tableName);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result) > 0;
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
        existsCommand.Parameters.AddWithValue("$migrationId", InitialMigrationId);

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
        insertCommand.Parameters.AddWithValue("$migrationId", InitialMigrationId);
        insertCommand.Parameters.AddWithValue("$productVersion", EfProductVersion);
        await insertCommand.ExecuteNonQueryAsync(cancellationToken);
    }
}
