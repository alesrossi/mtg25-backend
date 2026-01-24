using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedGitFieldsToDecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentBranchId",
                table: "Decks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentCommitId",
                table: "Decks",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Decks_CurrentBranchId",
                table: "Decks",
                column: "CurrentBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Decks_CurrentCommitId",
                table: "Decks",
                column: "CurrentCommitId");

            migrationBuilder.AddForeignKey(
                name: "FK_Decks_DeckBranches_CurrentBranchId",
                table: "Decks",
                column: "CurrentBranchId",
                principalTable: "DeckBranches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Decks_DeckCommits_CurrentCommitId",
                table: "Decks",
                column: "CurrentCommitId",
                principalTable: "DeckCommits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Decks_DeckBranches_CurrentBranchId",
                table: "Decks");

            migrationBuilder.DropForeignKey(
                name: "FK_Decks_DeckCommits_CurrentCommitId",
                table: "Decks");

            migrationBuilder.DropIndex(
                name: "IX_Decks_CurrentBranchId",
                table: "Decks");

            migrationBuilder.DropIndex(
                name: "IX_Decks_CurrentCommitId",
                table: "Decks");

            migrationBuilder.DropColumn(
                name: "CurrentBranchId",
                table: "Decks");

            migrationBuilder.DropColumn(
                name: "CurrentCommitId",
                table: "Decks");
        }
    }
}
