using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations.AppIdentityDb
{
    /// <inheritdoc />
    public partial class ChangedSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ReferencePrice",
                table: "Settings",
                type: "text",
                nullable: true,
                defaultValue: "Avg",
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "Avg");

            migrationBuilder.AlterColumn<string>(
                name: "MarketProvider",
                table: "Settings",
                type: "text",
                nullable: true,
                defaultValue: "Mkm",
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "Mkm");

            migrationBuilder.AlterColumn<string>(
                name: "LanguageUi",
                table: "Settings",
                type: "text",
                nullable: true,
                defaultValue: "It",
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "It");

            migrationBuilder.AlterColumn<string>(
                name: "LanguageCards",
                table: "Settings",
                type: "text",
                nullable: true,
                defaultValue: "En",
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "En");

            migrationBuilder.AlterColumn<bool>(
                name: "EnabledLocation",
                table: "Settings",
                type: "boolean",
                nullable: true,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "Settings",
                type: "text",
                nullable: true,
                defaultValue: "Eur",
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "Eur");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ReferencePrice",
                table: "Settings",
                type: "text",
                nullable: false,
                defaultValue: "Avg",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldDefaultValue: "Avg");

            migrationBuilder.AlterColumn<string>(
                name: "MarketProvider",
                table: "Settings",
                type: "text",
                nullable: false,
                defaultValue: "Mkm",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldDefaultValue: "Mkm");

            migrationBuilder.AlterColumn<string>(
                name: "LanguageUi",
                table: "Settings",
                type: "text",
                nullable: false,
                defaultValue: "It",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldDefaultValue: "It");

            migrationBuilder.AlterColumn<string>(
                name: "LanguageCards",
                table: "Settings",
                type: "text",
                nullable: false,
                defaultValue: "En",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldDefaultValue: "En");

            migrationBuilder.AlterColumn<bool>(
                name: "EnabledLocation",
                table: "Settings",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true,
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "Settings",
                type: "text",
                nullable: false,
                defaultValue: "Eur",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldDefaultValue: "Eur");
        }
    }
}
