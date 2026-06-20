using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Immo.Data.Migrations
{
    /// <inheritdoc />
    public partial class Jsonfilteringonproperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "JsonTypeFilterPath",
                table: "ParserConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JsonTypeFilterValues",
                table: "ParserConfigs",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JsonTypeFilterPath",
                table: "ParserConfigs");

            migrationBuilder.DropColumn(
                name: "JsonTypeFilterValues",
                table: "ParserConfigs");
        }
    }
}
