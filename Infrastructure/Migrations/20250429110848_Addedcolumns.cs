using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Addedcolumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Description",
                table: "Cards",
                newName: "Version");

            migrationBuilder.AddColumn<int>(
                name: "CollectionId",
                table: "Cards",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsFoil",
                table: "Cards",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "Cards",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "PurchasePrice",
                table: "Cards",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                table: "Cards",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Cards_CollectionId",
                table: "Cards",
                column: "CollectionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Cards_Collections_CollectionId",
                table: "Cards",
                column: "CollectionId",
                principalTable: "Collections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cards_Collections_CollectionId",
                table: "Cards");

            migrationBuilder.DropIndex(
                name: "IX_Cards_CollectionId",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "CollectionId",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "IsFoil",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "PurchasePrice",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "Cards");

            migrationBuilder.RenameColumn(
                name: "Version",
                table: "Cards",
                newName: "Description");
        }
    }
}
