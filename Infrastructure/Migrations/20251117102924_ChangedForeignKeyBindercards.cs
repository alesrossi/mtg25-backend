using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangedForeignKeyBindercards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BinderCards_Cards_CardId",
                table: "BinderCards");

            migrationBuilder.AddForeignKey(
                name: "FK_BinderCards_Cards_CardId",
                table: "BinderCards",
                column: "CardId",
                principalTable: "Cards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BinderCards_Cards_CardId",
                table: "BinderCards");

            migrationBuilder.AddForeignKey(
                name: "FK_BinderCards_Cards_CardId",
                table: "BinderCards",
                column: "CardId",
                principalTable: "Cards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
