using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations.AppIdentityDb
{
    /// <inheritdoc />
    public partial class ChangedNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsInstant",
                table: "Notifications");

            migrationBuilder.AlterColumn<bool>(
                name: "IsRead",
                table: "Notifications",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AddColumn<bool>(
                name: "Approval",
                table: "Notifications",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ObjectId",
                table: "Notifications",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Approval",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "ObjectId",
                table: "Notifications");

            migrationBuilder.AlterColumn<bool>(
                name: "IsRead",
                table: "Notifications",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsInstant",
                table: "Notifications",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
