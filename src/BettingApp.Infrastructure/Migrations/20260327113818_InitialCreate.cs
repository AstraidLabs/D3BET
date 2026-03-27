using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BettingApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BettingMarkets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    OpeningOdds = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    CurrentOdds = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BettingMarkets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Bettors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bettors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Bets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Odds = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    Stake = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    StakeCurrencyCode = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    StakeRealMoneyEquivalent = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    CreditToMoneyRateApplied = table.Column<decimal>(type: "TEXT", precision: 12, scale: 4, nullable: false),
                    MarketParticipationMultiplierApplied = table.Column<decimal>(type: "TEXT", precision: 12, scale: 4, nullable: false),
                    IsWinning = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    OutcomeStatus = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    IsPayoutProcessed = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    PayoutProcessedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PayoutCreditAmount = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    PayoutRealMoneyAmount = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    IsCommissionFeePaid = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    BettingMarketId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PlacedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BettorId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bets_BettingMarkets_BettingMarketId",
                        column: x => x.BettingMarketId,
                        principalTable: "BettingMarkets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bets_Bettors_BettorId",
                        column: x => x.BettorId,
                        principalTable: "Bettors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BettorWallets",
                columns: table => new
                {
                    BettorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Balance = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    CreditCode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    LastMoneyToCreditRate = table.Column<decimal>(type: "TEXT", precision: 12, scale: 4, nullable: false),
                    LastCreditToMoneyRate = table.Column<decimal>(type: "TEXT", precision: 12, scale: 4, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BettorWallets", x => x.BettorId);
                    table.ForeignKey(
                        name: "FK_BettorWallets_Bettors_BettorId",
                        column: x => x.BettorId,
                        principalTable: "Bettors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CreditWithdrawalRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BettorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreditAmount = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    RealMoneyAmount = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    RealCurrencyCode = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CreditToMoneyRateApplied = table.Column<decimal>(type: "TEXT", precision: 12, scale: 4, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Reference = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    ProcessedReason = table.Column<string>(type: "TEXT", maxLength: 240, nullable: true),
                    IsAutoProcessed = table.Column<bool>(type: "INTEGER", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditWithdrawalRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditWithdrawalRequests_Bettors_BettorId",
                        column: x => x.BettorId,
                        principalTable: "Bettors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "D3CreditTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BettorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    CreditAmount = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    RealMoneyAmount = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    RealCurrencyCode = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    MoneyToCreditRate = table.Column<decimal>(type: "TEXT", precision: 12, scale: 4, nullable: false),
                    CreditToMoneyRate = table.Column<decimal>(type: "TEXT", precision: 12, scale: 4, nullable: false),
                    MarketParticipationMultiplier = table.Column<decimal>(type: "TEXT", precision: 12, scale: 4, nullable: false),
                    Reference = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_D3CreditTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_D3CreditTransactions_Bettors_BettorId",
                        column: x => x.BettorId,
                        principalTable: "Bettors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ElectronicReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BettorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    DocumentNumber = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    CreditAmount = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    RealMoneyAmount = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    RealCurrencyCode = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    MoneyToCreditRate = table.Column<decimal>(type: "TEXT", precision: 12, scale: 4, nullable: false),
                    CreditToMoneyRate = table.Column<decimal>(type: "TEXT", precision: 12, scale: 4, nullable: false),
                    Reference = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    RelatedTransactionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RelatedBetId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RelatedWithdrawalRequestId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IssuedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectronicReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ElectronicReceipts_Bettors_BettorId",
                        column: x => x.BettorId,
                        principalTable: "Bettors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bets_BettingMarketId",
                table: "Bets",
                column: "BettingMarketId");

            migrationBuilder.CreateIndex(
                name: "IX_Bets_BettorId",
                table: "Bets",
                column: "BettorId");

            migrationBuilder.CreateIndex(
                name: "IX_Bettors_Name",
                table: "Bettors",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditWithdrawalRequests_BettorId_RequestedAtUtc",
                table: "CreditWithdrawalRequests",
                columns: new[] { "BettorId", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditWithdrawalRequests_Status",
                table: "CreditWithdrawalRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_D3CreditTransactions_BettorId_CreatedAtUtc",
                table: "D3CreditTransactions",
                columns: new[] { "BettorId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicReceipts_BettorId_IssuedAtUtc",
                table: "ElectronicReceipts",
                columns: new[] { "BettorId", "IssuedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicReceipts_DocumentNumber",
                table: "ElectronicReceipts",
                column: "DocumentNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bets");

            migrationBuilder.DropTable(
                name: "BettorWallets");

            migrationBuilder.DropTable(
                name: "CreditWithdrawalRequests");

            migrationBuilder.DropTable(
                name: "D3CreditTransactions");

            migrationBuilder.DropTable(
                name: "ElectronicReceipts");

            migrationBuilder.DropTable(
                name: "BettingMarkets");

            migrationBuilder.DropTable(
                name: "Bettors");
        }
    }
}
