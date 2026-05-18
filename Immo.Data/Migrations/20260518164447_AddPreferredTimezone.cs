using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Immo.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPreferredTimezone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreferredTimezone",
                table: "AppSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "AppSettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "PreferredTimezone",
                value: "UTC");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreferredTimezone",
                table: "AppSettings");
        }
    }
}
