using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedTeamEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TeamId",
                table: "Wishlists",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TeamId",
                table: "TradeBinders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TeamId",
                table: "Decks",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OwnerId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Teams_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeamCollectionViews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TeamId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CollectionIds = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamCollectionViews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamCollectionViews_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamCollectionViews_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeamMember",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TeamId = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    InvitedById = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamMember", x => new { x.UserId, x.TeamId });
                    table.ForeignKey(
                        name: "FK_TeamMember_AspNetUsers_InvitedById",
                        column: x => x.InvitedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamMember_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeamMember_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Wishlists_TeamId",
                table: "Wishlists",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeBinders_TeamId",
                table: "TradeBinders",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Decks_TeamId",
                table: "Decks",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamCollectionViews_CreatedById",
                table: "TeamCollectionViews",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_TeamCollectionViews_TeamId",
                table: "TeamCollectionViews",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMember_InvitedById",
                table: "TeamMember",
                column: "InvitedById");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMember_TeamId_UserId",
                table: "TeamMember",
                columns: new[] { "TeamId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Teams_OwnerId",
                table: "Teams",
                column: "OwnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Decks_Teams_TeamId",
                table: "Decks",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TradeBinders_Teams_TeamId",
                table: "TradeBinders",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Wishlists_Teams_TeamId",
                table: "Wishlists",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Decks_Teams_TeamId",
                table: "Decks");

            migrationBuilder.DropForeignKey(
                name: "FK_TradeBinders_Teams_TeamId",
                table: "TradeBinders");

            migrationBuilder.DropForeignKey(
                name: "FK_Wishlists_Teams_TeamId",
                table: "Wishlists");

            migrationBuilder.DropTable(
                name: "TeamCollectionViews");

            migrationBuilder.DropTable(
                name: "TeamMember");

            migrationBuilder.DropTable(
                name: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_Wishlists_TeamId",
                table: "Wishlists");

            migrationBuilder.DropIndex(
                name: "IX_TradeBinders_TeamId",
                table: "TradeBinders");

            migrationBuilder.DropIndex(
                name: "IX_Decks_TeamId",
                table: "Decks");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "Wishlists");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "TradeBinders");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "Decks");
        }
    }
}
