using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BettingApp.Infrastructure.Migrations
{
    public partial class AddBettingMarkets : Migration
    {
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

            migrationBuilder.AddColumn<Guid>(
                name: "BettingMarketId",
                table: "Bets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bets_BettingMarketId",
                table: "Bets",
                column: "BettingMarketId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bets_BettingMarkets_BettingMarketId",
                table: "Bets",
                column: "BettingMarketId",
                principalTable: "BettingMarkets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bets_BettingMarkets_BettingMarketId",
                table: "Bets");

            migrationBuilder.DropIndex(
                name: "IX_Bets_BettingMarketId",
                table: "Bets");

            migrationBuilder.DropColumn(
                name: "BettingMarketId",
                table: "Bets");

            migrationBuilder.DropTable(
                name: "BettingMarkets");
        }
    }
}
