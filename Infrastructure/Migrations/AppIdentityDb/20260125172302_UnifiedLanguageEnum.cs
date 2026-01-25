using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations.AppIdentityDb
{
    /// <inheritdoc />
    public partial class UnifiedLanguageEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "LanguageUi",
                table: "Settings",
                type: "text",
                nullable: false,
                defaultValue: "it",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldDefaultValue: "It");

            migrationBuilder.AlterColumn<string>(
                name: "LanguageCards",
                table: "Settings",
                type: "text",
                nullable: false,
                defaultValue: "en",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldDefaultValue: "En");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "LanguageUi",
                table: "Settings",
                type: "text",
                nullable: true,
                defaultValue: "It",
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "it");

            migrationBuilder.AlterColumn<string>(
                name: "LanguageCards",
                table: "Settings",
                type: "text",
                nullable: true,
                defaultValue: "En",
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "en");
        }
    }
}
