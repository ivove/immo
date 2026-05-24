using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Immo.Data.Migrations
{
    /// <inheritdoc />
    public partial class addedsmtpsettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FromEmail",
                table: "AppSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SmtpHost",
                table: "AppSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SmtpPassword",
                table: "AppSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SmtpPort",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "SmtpUseSsl",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SmtpUsername",
                table: "AppSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "AppSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FromEmail", "SmtpHost", "SmtpPassword", "SmtpPort", "SmtpUseSsl", "SmtpUsername" },
                values: new object[] { "", "", "", 25, true, "" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FromEmail",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "SmtpHost",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "SmtpPassword",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "SmtpPort",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "SmtpUseSsl",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "SmtpUsername",
                table: "AppSettings");
        }
    }
}
