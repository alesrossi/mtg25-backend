using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations.AppIdentityDb
{
    /// <inheritdoc />
    public partial class ChangedImplementation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppUserLeagues");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Leagues",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                table: "Leagues",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Format",
                table: "Leagues",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "UserLeagues",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    LeagueId = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLeagues", x => new { x.UserId, x.LeagueId });
                    table.ForeignKey(
                        name: "FK_UserLeagues_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserLeagues_Leagues_LeagueId",
                        column: x => x.LeagueId,
                        principalTable: "Leagues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserLeagues_LeagueId",
                table: "UserLeagues",
                column: "LeagueId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserLeagues");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Leagues");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "Leagues");

            migrationBuilder.DropColumn(
                name: "Format",
                table: "Leagues");

            migrationBuilder.CreateTable(
                name: "AppUserLeagues",
                columns: table => new
                {
                    LeaguesId = table.Column<int>(type: "integer", nullable: false),
                    UsersId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUserLeagues", x => new { x.LeaguesId, x.UsersId });
                    table.ForeignKey(
                        name: "FK_AppUserLeagues_AspNetUsers_UsersId",
                        column: x => x.UsersId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppUserLeagues_Leagues_LeaguesId",
                        column: x => x.LeaguesId,
                        principalTable: "Leagues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppUserLeagues_UsersId",
                table: "AppUserLeagues",
                column: "UsersId");
        }
    }
}
