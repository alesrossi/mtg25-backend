using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations.AppIdentityDb
{
    /// <inheritdoc />
    public partial class AddedExtraColumns2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Stack<int>>(
                name: "Rounds",
                table: "UserLeagues",
                type: "integer[]",
                nullable: true,
                oldClrType: typeof(Stack<int>),
                oldType: "integer[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Stack<int>>(
                name: "Rounds",
                table: "UserLeagues",
                type: "integer[]",
                nullable: false,
                oldClrType: typeof(Stack<int>),
                oldType: "integer[]",
                oldNullable: true);
        }
    }
}
