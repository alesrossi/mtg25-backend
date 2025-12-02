using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedBackImageFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BackImageUrl",
                table: "WishlistCards",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "WishlistCards",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BackImageUrl",
                table: "DeckCards",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BackImageUrl",
                table: "WishlistCards");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "WishlistCards");

            migrationBuilder.DropColumn(
                name: "BackImageUrl",
                table: "DeckCards");
        }
    }
}
