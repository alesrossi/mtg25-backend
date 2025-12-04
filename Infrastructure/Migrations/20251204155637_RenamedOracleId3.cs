using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenamedOracleId3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "OracleId",
                table: "WishlistCards",
                newName: "ScryfallId");

            migrationBuilder.RenameColumn(
                name: "OracleId",
                table: "DeckCards",
                newName: "ScryfallId");

            migrationBuilder.RenameColumn(
                name: "OracleId",
                table: "Cards",
                newName: "ScryfallId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ScryfallId",
                table: "WishlistCards",
                newName: "OracleId");

            migrationBuilder.RenameColumn(
                name: "ScryfallId",
                table: "DeckCards",
                newName: "OracleId");

            migrationBuilder.RenameColumn(
                name: "ScryfallId",
                table: "Cards",
                newName: "OracleId");
        }
    }
}
