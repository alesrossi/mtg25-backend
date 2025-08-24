using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations.AppIdentityDb
{
    /// <inheritdoc />
    public partial class AddedExtraColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AvgScore",
                table: "UserLeagues",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "BestRound",
                table: "UserLeagues",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Stack<int>>(
                name: "Rounds",
                table: "UserLeagues",
                type: "integer[]",
                nullable: false);

            migrationBuilder.AddColumn<int>(
                name: "RoundsPlayed",
                table: "UserLeagues",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Leagues",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MinimumRounds",
                table: "Leagues",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RoundsToConsider",
                table: "Leagues",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalPlayers",
                table: "Leagues",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalRounds",
                table: "Leagues",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvgScore",
                table: "UserLeagues");

            migrationBuilder.DropColumn(
                name: "BestRound",
                table: "UserLeagues");

            migrationBuilder.DropColumn(
                name: "Rounds",
                table: "UserLeagues");

            migrationBuilder.DropColumn(
                name: "RoundsPlayed",
                table: "UserLeagues");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Leagues");

            migrationBuilder.DropColumn(
                name: "MinimumRounds",
                table: "Leagues");

            migrationBuilder.DropColumn(
                name: "RoundsToConsider",
                table: "Leagues");

            migrationBuilder.DropColumn(
                name: "TotalPlayers",
                table: "Leagues");

            migrationBuilder.DropColumn(
                name: "TotalRounds",
                table: "Leagues");
        }
    }
}
