using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangedFKGitDeckEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeckBranches_DeckCommits_HeadCommitId",
                table: "DeckBranches");

            migrationBuilder.DropForeignKey(
                name: "FK_DeckCommitParents_DeckCommits_ParentCommitId",
                table: "DeckCommitParents");

            migrationBuilder.AddForeignKey(
                name: "FK_DeckBranches_DeckCommits_HeadCommitId",
                table: "DeckBranches",
                column: "HeadCommitId",
                principalTable: "DeckCommits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_DeckCommitParents_DeckCommits_ParentCommitId",
                table: "DeckCommitParents",
                column: "ParentCommitId",
                principalTable: "DeckCommits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeckBranches_DeckCommits_HeadCommitId",
                table: "DeckBranches");

            migrationBuilder.DropForeignKey(
                name: "FK_DeckCommitParents_DeckCommits_ParentCommitId",
                table: "DeckCommitParents");

            migrationBuilder.AddForeignKey(
                name: "FK_DeckBranches_DeckCommits_HeadCommitId",
                table: "DeckBranches",
                column: "HeadCommitId",
                principalTable: "DeckCommits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeckCommitParents_DeckCommits_ParentCommitId",
                table: "DeckCommitParents",
                column: "ParentCommitId",
                principalTable: "DeckCommits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
