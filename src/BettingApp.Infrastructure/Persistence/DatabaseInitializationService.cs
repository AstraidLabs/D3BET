using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace BettingApp.Infrastructure.Persistence;

public sealed class DatabaseInitializationService(BettingDbContext dbContext)
{
    private const string CurrentBaselineMigrationId = "20260327113818_InitialCreate";
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

        var hasMigrationHistoryTable = await HasMigrationHistoryTableAsync(connection, cancellationToken);
        var hasLegacyTables = await HasLegacyTablesAsync(connection, cancellationToken);
        if (!hasLegacyTables)
        {
            return;
        }

        if (!hasMigrationHistoryTable)
        {
            await EnsureMigrationHistoryTableAsync(connection, cancellationToken);
        }

        await EnsureWinningColumnAsync(connection, cancellationToken);
        await EnsureCommissionFeePaidColumnAsync(connection, cancellationToken);
        await EnsureOutcomeStatusColumnAsync(connection, cancellationToken);
        await EnsureBettorsTableAsync(connection, cancellationToken);
        await EnsureBettingMarketsTableAsync(connection, cancellationToken);
        await EnsureBetsTableAsync(connection, cancellationToken);
        await EnsureBettingMarketIdColumnAsync(connection, cancellationToken);
        await EnsureD3CreditColumnsAsync(connection, cancellationToken);
        await EnsureBetPayoutColumnsAsync(connection, cancellationToken);
        await EnsureBettorWalletsTableAsync(connection, cancellationToken);
        await EnsureD3CreditTransactionsTableAsync(connection, cancellationToken);
        await EnsureCreditWithdrawalRequestsTableAsync(connection, cancellationToken);
        await EnsureElectronicReceiptsTableAsync(connection, cancellationToken);
        await EnsureIndexesAsync(connection, cancellationToken);
        await EnsureMigrationHistoryRowAsync(connection, CurrentBaselineMigrationId, cancellationToken);
    }

    private async Task EnsureSchemaCompatibilityAsync(CancellationToken cancellationToken)
    {
        await using var connection = (SqliteConnection)dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await EnsureBettorsTableAsync(connection, cancellationToken);
        await EnsureBettingMarketsTableAsync(connection, cancellationToken);
        await EnsureBetsTableAsync(connection, cancellationToken);
        await EnsureWinningColumnAsync(connection, cancellationToken);
        await EnsureOutcomeStatusColumnAsync(connection, cancellationToken);
        await EnsureCommissionFeePaidColumnAsync(connection, cancellationToken);
        await EnsureBettingMarketIdColumnAsync(connection, cancellationToken);
        await EnsureD3CreditColumnsAsync(connection, cancellationToken);
        await EnsureBetPayoutColumnsAsync(connection, cancellationToken);
        await EnsureBettorWalletsTableAsync(connection, cancellationToken);
        await EnsureD3CreditTransactionsTableAsync(connection, cancellationToken);
        await EnsureCreditWithdrawalRequestsTableAsync(connection, cancellationToken);
        await EnsureElectronicReceiptsTableAsync(connection, cancellationToken);
        await EnsureIndexesAsync(connection, cancellationToken);
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

    private static async Task EnsureMigrationHistoryRowAsync(SqliteConnection connection, string migrationId, CancellationToken cancellationToken)
    {
        await using var existsCommand = connection.CreateCommand();
        existsCommand.CommandText = """
            SELECT COUNT(*)
            FROM "__EFMigrationsHistory"
            WHERE "MigrationId" = $migrationId;
            """;
        existsCommand.Parameters.AddWithValue("$migrationId", migrationId);

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
        insertCommand.Parameters.AddWithValue("$migrationId", migrationId);
        insertCommand.Parameters.AddWithValue("$productVersion", EfProductVersion);
        await insertCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureD3CreditColumnsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        if (!await HasTableAsync(connection, "Bets", cancellationToken))
        {
            return;
        }

        await EnsureColumnAsync(connection, "Bets", "StakeCurrencyCode", "ALTER TABLE Bets ADD COLUMN StakeCurrencyCode TEXT NOT NULL DEFAULT 'CZK';", cancellationToken);
        await EnsureColumnAsync(connection, "Bets", "StakeRealMoneyEquivalent", "ALTER TABLE Bets ADD COLUMN StakeRealMoneyEquivalent TEXT NOT NULL DEFAULT 0.0;", cancellationToken);
        await EnsureColumnAsync(connection, "Bets", "CreditToMoneyRateApplied", "ALTER TABLE Bets ADD COLUMN CreditToMoneyRateApplied TEXT NOT NULL DEFAULT 1.0;", cancellationToken);
        await EnsureColumnAsync(connection, "Bets", "MarketParticipationMultiplierApplied", "ALTER TABLE Bets ADD COLUMN MarketParticipationMultiplierApplied TEXT NOT NULL DEFAULT 1.0;", cancellationToken);
    }

    private static async Task EnsureBetPayoutColumnsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        if (!await HasTableAsync(connection, "Bets", cancellationToken))
        {
            return;
        }

        await EnsureColumnAsync(connection, "Bets", "IsPayoutProcessed", "ALTER TABLE Bets ADD COLUMN IsPayoutProcessed INTEGER NOT NULL DEFAULT 0;", cancellationToken);
        await EnsureColumnAsync(connection, "Bets", "PayoutProcessedAtUtc", "ALTER TABLE Bets ADD COLUMN PayoutProcessedAtUtc TEXT NULL;", cancellationToken);
        await EnsureColumnAsync(connection, "Bets", "PayoutCreditAmount", "ALTER TABLE Bets ADD COLUMN PayoutCreditAmount TEXT NOT NULL DEFAULT 0.0;", cancellationToken);
        await EnsureColumnAsync(connection, "Bets", "PayoutRealMoneyAmount", "ALTER TABLE Bets ADD COLUMN PayoutRealMoneyAmount TEXT NOT NULL DEFAULT 0.0;", cancellationToken);
    }

    private static async Task EnsureBettorWalletsTableAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS "BettorWallets" (
                "BettorId" TEXT NOT NULL CONSTRAINT "PK_BettorWallets" PRIMARY KEY,
                "Balance" TEXT NOT NULL,
                "CreditCode" TEXT NOT NULL,
                "LastMoneyToCreditRate" TEXT NOT NULL,
                "LastCreditToMoneyRate" TEXT NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "UpdatedAtUtc" TEXT NOT NULL,
                CONSTRAINT "FK_BettorWallets_Bettors_BettorId" FOREIGN KEY ("BettorId") REFERENCES "Bettors" ("Id") ON DELETE CASCADE
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureBettorsTableAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS "Bettors" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Bettors" PRIMARY KEY,
                "Name" TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureBetsTableAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS "Bets" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Bets" PRIMARY KEY,
                "EventName" TEXT NOT NULL,
                "Odds" TEXT NOT NULL,
                "Stake" TEXT NOT NULL,
                "StakeCurrencyCode" TEXT NOT NULL DEFAULT 'CZK',
                "StakeRealMoneyEquivalent" TEXT NOT NULL DEFAULT 0.0,
                "CreditToMoneyRateApplied" TEXT NOT NULL DEFAULT 1.0,
                "MarketParticipationMultiplierApplied" TEXT NOT NULL DEFAULT 1.0,
                "IsWinning" INTEGER NOT NULL DEFAULT 0,
                "OutcomeStatus" INTEGER NOT NULL DEFAULT 0,
                "IsCommissionFeePaid" INTEGER NOT NULL DEFAULT 0,
                "BettingMarketId" TEXT NULL,
                "PlacedAtUtc" TEXT NOT NULL,
                "BettorId" TEXT NOT NULL,
                CONSTRAINT "FK_Bets_BettingMarkets_BettingMarketId" FOREIGN KEY ("BettingMarketId") REFERENCES "BettingMarkets" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_Bets_Bettors_BettorId" FOREIGN KEY ("BettorId") REFERENCES "Bettors" ("Id") ON DELETE RESTRICT
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureD3CreditTransactionsTableAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS "D3CreditTransactions" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_D3CreditTransactions" PRIMARY KEY,
                "BettorId" TEXT NOT NULL,
                "Type" INTEGER NOT NULL,
                "CreditAmount" TEXT NOT NULL,
                "RealMoneyAmount" TEXT NOT NULL,
                "RealCurrencyCode" TEXT NOT NULL,
                "MoneyToCreditRate" TEXT NOT NULL,
                "CreditToMoneyRate" TEXT NOT NULL,
                "MarketParticipationMultiplier" TEXT NOT NULL,
                "Reference" TEXT NOT NULL,
                "Description" TEXT NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                CONSTRAINT "FK_D3CreditTransactions_Bettors_BettorId" FOREIGN KEY ("BettorId") REFERENCES "Bettors" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_D3CreditTransactions_BettorId_CreatedAtUtc" ON "D3CreditTransactions" ("BettorId", "CreatedAtUtc");
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureCreditWithdrawalRequestsTableAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS "CreditWithdrawalRequests" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_CreditWithdrawalRequests" PRIMARY KEY,
                "BettorId" TEXT NOT NULL,
                "CreditAmount" TEXT NOT NULL,
                "RealMoneyAmount" TEXT NOT NULL,
                "RealCurrencyCode" TEXT NOT NULL,
                "CreditToMoneyRateApplied" TEXT NOT NULL,
                "Status" INTEGER NOT NULL,
                "Reference" TEXT NOT NULL,
                "Reason" TEXT NOT NULL,
                "ProcessedReason" TEXT NULL,
                "IsAutoProcessed" INTEGER NOT NULL DEFAULT 0,
                "RequestedAtUtc" TEXT NOT NULL,
                "ProcessedAtUtc" TEXT NULL,
                CONSTRAINT "FK_CreditWithdrawalRequests_Bettors_BettorId" FOREIGN KEY ("BettorId") REFERENCES "Bettors" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_CreditWithdrawalRequests_BettorId_RequestedAtUtc" ON "CreditWithdrawalRequests" ("BettorId", "RequestedAtUtc");
            CREATE INDEX IF NOT EXISTS "IX_CreditWithdrawalRequests_Status" ON "CreditWithdrawalRequests" ("Status");
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureElectronicReceiptsTableAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS "ElectronicReceipts" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_ElectronicReceipts" PRIMARY KEY,
                "BettorId" TEXT NOT NULL,
                "Type" INTEGER NOT NULL,
                "DocumentNumber" TEXT NOT NULL,
                "Title" TEXT NOT NULL,
                "Summary" TEXT NOT NULL,
                "CreditAmount" TEXT NOT NULL,
                "RealMoneyAmount" TEXT NOT NULL,
                "RealCurrencyCode" TEXT NOT NULL,
                "MoneyToCreditRate" TEXT NOT NULL,
                "CreditToMoneyRate" TEXT NOT NULL,
                "Reference" TEXT NOT NULL,
                "RelatedTransactionId" TEXT NULL,
                "RelatedBetId" TEXT NULL,
                "RelatedWithdrawalRequestId" TEXT NULL,
                "IssuedAtUtc" TEXT NOT NULL,
                CONSTRAINT "FK_ElectronicReceipts_Bettors_BettorId" FOREIGN KEY ("BettorId") REFERENCES "Bettors" ("Id") ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_ElectronicReceipts_DocumentNumber" ON "ElectronicReceipts" ("DocumentNumber");
            CREATE INDEX IF NOT EXISTS "IX_ElectronicReceipts_BettorId_IssuedAtUtc" ON "ElectronicReceipts" ("BettorId", "IssuedAtUtc");
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureIndexesAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Bettors_Name" ON "Bettors" ("Name");
            CREATE INDEX IF NOT EXISTS "IX_Bets_BettingMarketId" ON "Bets" ("BettingMarketId");
            CREATE INDEX IF NOT EXISTS "IX_Bets_BettorId" ON "Bets" ("BettorId");
            CREATE INDEX IF NOT EXISTS "IX_Bets_OutcomeStatus_IsPayoutProcessed" ON "Bets" ("OutcomeStatus", "IsPayoutProcessed");
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureColumnAsync(
        SqliteConnection connection,
        string tableName,
        string columnName,
        string alterSql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info('{tableName}');";

        var hasColumn = false;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                hasColumn = true;
                break;
            }
        }

        if (hasColumn)
        {
            return;
        }

        await using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = alterSql;
        await alterCommand.ExecuteNonQueryAsync(cancellationToken);
    }
}
