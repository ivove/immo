using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Immo.Data.Migrations
{
    /// <inheritdoc />
    public partial class addedlinktoagencyonrawpageandproperty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AgencyId",
                table: "RawPages",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AgencyId",
                table: "Properties",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RawPages_AgencyId",
                table: "RawPages",
                column: "AgencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_AgencyId",
                table: "Properties",
                column: "AgencyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Properties_Agencies_AgencyId",
                table: "Properties",
                column: "AgencyId",
                principalTable: "Agencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_RawPages_Agencies_AgencyId",
                table: "RawPages",
                column: "AgencyId",
                principalTable: "Agencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Properties_Agencies_AgencyId",
                table: "Properties");

            migrationBuilder.DropForeignKey(
                name: "FK_RawPages_Agencies_AgencyId",
                table: "RawPages");

            migrationBuilder.DropIndex(
                name: "IX_RawPages_AgencyId",
                table: "RawPages");

            migrationBuilder.DropIndex(
                name: "IX_Properties_AgencyId",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "AgencyId",
                table: "RawPages");

            migrationBuilder.DropColumn(
                name: "AgencyId",
                table: "Properties");
        }
    }
}
