using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Immo.Data.Migrations
{
    /// <inheritdoc />
    public partial class addpricetoparserconfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PriceLabel",
                table: "ParserConfigs",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PriceLabel",
                table: "ParserConfigs");
        }
    }
}
