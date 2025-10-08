using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedColorIdentityToDeckCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "ColorIdentity",
                table: "DeckCards",
                type: "text[]",
                nullable: true);

            migrationBuilder.Sql("UPDATE \"DeckCards\" SET \"ColorIdentity\" = '{}'::text[] WHERE \"ColorIdentity\" IS NULL;");

            migrationBuilder.AlterColumn<List<string>>(
                name: "ColorIdentity",
                table: "DeckCards",
                type: "text[]",
                nullable: false,
                oldClrType: typeof(List<string>),
                oldType: "text[]",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ColorIdentity",
                table: "DeckCards");
        }
    }
}
