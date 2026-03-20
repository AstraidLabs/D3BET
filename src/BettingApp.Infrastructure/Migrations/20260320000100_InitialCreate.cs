using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BettingApp.Infrastructure.Migrations;

public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
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
                IsWinning = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                PlacedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                BettorId = table.Column<Guid>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Bets", x => x.Id);
                table.ForeignKey(
                    name: "FK_Bets_Bettors_BettorId",
                    column: x => x.BettorId,
                    principalTable: "Bettors",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Bets_BettorId",
            table: "Bets",
            column: "BettorId");

        migrationBuilder.CreateIndex(
            name: "IX_Bettors_Name",
            table: "Bettors",
            column: "Name",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Bets");

        migrationBuilder.DropTable(
            name: "Bettors");
    }
}
