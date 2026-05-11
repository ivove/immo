using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Immo.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgencyCrawlRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CrawlRequestedAt",
                table: "Agencies",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CrawlRequestedAt",
                table: "Agencies");
        }
    }
}
