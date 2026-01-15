using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations.AppIdentityDb
{
    /// <inheritdoc />
    public partial class AddedRoundExtraInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Draws",
                table: "UserRounds",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Gw",
                table: "UserRounds",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Losses",
                table: "UserRounds",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Ogw",
                table: "UserRounds",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Omw",
                table: "UserRounds",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Wins",
                table: "UserRounds",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Draws",
                table: "UserRounds");

            migrationBuilder.DropColumn(
                name: "Gw",
                table: "UserRounds");

            migrationBuilder.DropColumn(
                name: "Losses",
                table: "UserRounds");

            migrationBuilder.DropColumn(
                name: "Ogw",
                table: "UserRounds");

            migrationBuilder.DropColumn(
                name: "Omw",
                table: "UserRounds");

            migrationBuilder.DropColumn(
                name: "Wins",
                table: "UserRounds");
        }
    }
}
