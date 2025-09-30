using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedWishListEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CollectorNumber",
                table: "WishlistCards");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "WishlistCards");

            migrationBuilder.DropColumn(
                name: "Rarity",
                table: "WishlistCards");

            migrationBuilder.DropColumn(
                name: "SetCode",
                table: "WishlistCards");

            migrationBuilder.DropColumn(
                name: "SetName",
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

            migrationBuilder.AlterColumn<bool>(
                name: "IsFoil",
                table: "WishlistCards",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "WishlistCards",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<bool>(
                name: "IsFoil",
                table: "WishlistCards",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CollectorNumber",
                table: "WishlistCards",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "WishlistCards",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Rarity",
                table: "WishlistCards",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SetCode",
                table: "WishlistCards",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SetName",
                table: "WishlistCards",
                type: "text",
                nullable: true);
        }
    }
}
