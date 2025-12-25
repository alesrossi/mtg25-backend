using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations.AppIdentityDb
{
    /// <inheritdoc />
    public partial class NewScoringSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<List<int>>(
                name: "PointsToGive",
                table: "Leagues",
                type: "integer[]",
                nullable: true,
                oldClrType: typeof(List<int>),
                oldType: "integer[]");

            migrationBuilder.AddColumn<int>(
                name: "PointsPerDraw",
                table: "Leagues",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PointsPerLoss",
                table: "Leagues",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PointsPerWin",
                table: "Leagues",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScoringSystem",
                table: "Leagues",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PointsPerDraw",
                table: "Leagues");

            migrationBuilder.DropColumn(
                name: "PointsPerLoss",
                table: "Leagues");

            migrationBuilder.DropColumn(
                name: "PointsPerWin",
                table: "Leagues");

            migrationBuilder.DropColumn(
                name: "ScoringSystem",
                table: "Leagues");

            migrationBuilder.AlterColumn<List<int>>(
                name: "PointsToGive",
                table: "Leagues",
                type: "integer[]",
                nullable: false,
                oldClrType: typeof(List<int>),
                oldType: "integer[]",
                oldNullable: true);
        }
    }
}
