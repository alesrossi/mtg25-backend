using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedExtrafieldsToWishlistcards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "WishlistCards",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "ArtCrop",
                table: "WishlistCards",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimumCondition",
                table: "WishlistCards",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArtCrop",
                table: "WishlistCards");

            migrationBuilder.DropColumn(
                name: "MinimumCondition",
                table: "WishlistCards");

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "WishlistCards",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
