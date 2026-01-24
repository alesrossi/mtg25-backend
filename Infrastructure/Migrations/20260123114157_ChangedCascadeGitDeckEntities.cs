using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangedCascadeGitDeckEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeckCommits_DeckTrees_TreeId",
                table: "DeckCommits");

            migrationBuilder.AddForeignKey(
                name: "FK_DeckCommits_DeckTrees_TreeId",
                table: "DeckCommits",
                column: "TreeId",
                principalTable: "DeckTrees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeckCommits_DeckTrees_TreeId",
                table: "DeckCommits");

            migrationBuilder.AddForeignKey(
                name: "FK_DeckCommits_DeckTrees_TreeId",
                table: "DeckCommits",
                column: "TreeId",
                principalTable: "DeckTrees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
