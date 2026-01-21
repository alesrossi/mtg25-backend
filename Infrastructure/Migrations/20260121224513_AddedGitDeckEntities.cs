using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedGitDeckEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeckTrees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TreeHash = table.Column<byte[]>(type: "bytea", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeckTrees", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeckCommits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeckId = table.Column<int>(type: "integer", nullable: false),
                    TreeId = table.Column<int>(type: "integer", nullable: false),
                    AuthorId = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    CommittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ContentHash = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeckCommits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeckCommits_DeckTrees_TreeId",
                        column: x => x.TreeId,
                        principalTable: "DeckTrees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeckCommits_Decks_DeckId",
                        column: x => x.DeckId,
                        principalTable: "Decks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeckTreeEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TreeId = table.Column<int>(type: "integer", nullable: false),
                    ScryfallId = table.Column<string>(type: "text", nullable: false),
                    MaindeckQuantity = table.Column<int>(type: "integer", nullable: false),
                    SideboardQuantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeckTreeEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeckTreeEntries_DeckTrees_TreeId",
                        column: x => x.TreeId,
                        principalTable: "DeckTrees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeckBranches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeckId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    HeadCommitId = table.Column<int>(type: "integer", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeckBranches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeckBranches_DeckCommits_HeadCommitId",
                        column: x => x.HeadCommitId,
                        principalTable: "DeckCommits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeckBranches_Decks_DeckId",
                        column: x => x.DeckId,
                        principalTable: "Decks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeckCommitParents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CommitId = table.Column<int>(type: "integer", nullable: false),
                    ParentCommitId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeckCommitParents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeckCommitParents_DeckCommits_CommitId",
                        column: x => x.CommitId,
                        principalTable: "DeckCommits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeckCommitParents_DeckCommits_ParentCommitId",
                        column: x => x.ParentCommitId,
                        principalTable: "DeckCommits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeckBranches_DeckId_Name",
                table: "DeckBranches",
                columns: new[] { "DeckId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeckBranches_HeadCommitId",
                table: "DeckBranches",
                column: "HeadCommitId");

            migrationBuilder.CreateIndex(
                name: "IX_DeckCommitParents_CommitId_ParentCommitId",
                table: "DeckCommitParents",
                columns: new[] { "CommitId", "ParentCommitId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeckCommitParents_ParentCommitId",
                table: "DeckCommitParents",
                column: "ParentCommitId");

            migrationBuilder.CreateIndex(
                name: "IX_DeckCommits_DeckId",
                table: "DeckCommits",
                column: "DeckId");

            migrationBuilder.CreateIndex(
                name: "IX_DeckCommits_TreeId",
                table: "DeckCommits",
                column: "TreeId");

            migrationBuilder.CreateIndex(
                name: "IX_DeckTreeEntries_TreeId_ScryfallId",
                table: "DeckTreeEntries",
                columns: new[] { "TreeId", "ScryfallId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeckBranches");

            migrationBuilder.DropTable(
                name: "DeckCommitParents");

            migrationBuilder.DropTable(
                name: "DeckTreeEntries");

            migrationBuilder.DropTable(
                name: "DeckCommits");

            migrationBuilder.DropTable(
                name: "DeckTrees");
        }
    }
}
