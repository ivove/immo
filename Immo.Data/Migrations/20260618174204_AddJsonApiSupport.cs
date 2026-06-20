using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Immo.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJsonApiSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "JsonArrayPath",
                table: "ParserConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JsonBedroomsPath",
                table: "ParserConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JsonCityPath",
                table: "ParserConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JsonDescriptionPath",
                table: "ParserConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JsonEpcPath",
                table: "ParserConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JsonExternalIdPath",
                table: "ParserConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JsonImagePath",
                table: "ParserConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JsonLivingAreaPath",
                table: "ParserConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JsonPlotAreaPath",
                table: "ParserConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JsonPricePath",
                table: "ParserConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JsonSoldValue",
                table: "ParserConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JsonStatusPath",
                table: "ParserConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JsonTitlePath",
                table: "ParserConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JsonUnderOptionValue",
                table: "ParserConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JsonUrlPath",
                table: "ParserConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JsonZipCodePath",
                table: "ParserConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApiListingUrl",
                table: "Agencies",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataSourceType",
                table: "Agencies",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JsonArrayPath",
                table: "ParserConfigs");

            migrationBuilder.DropColumn(
                name: "JsonBedroomsPath",
                table: "ParserConfigs");

            migrationBuilder.DropColumn(
                name: "JsonCityPath",
                table: "ParserConfigs");

            migrationBuilder.DropColumn(
                name: "JsonDescriptionPath",
                table: "ParserConfigs");

            migrationBuilder.DropColumn(
                name: "JsonEpcPath",
                table: "ParserConfigs");

            migrationBuilder.DropColumn(
                name: "JsonExternalIdPath",
                table: "ParserConfigs");

            migrationBuilder.DropColumn(
                name: "JsonImagePath",
                table: "ParserConfigs");

            migrationBuilder.DropColumn(
                name: "JsonLivingAreaPath",
                table: "ParserConfigs");

            migrationBuilder.DropColumn(
                name: "JsonPlotAreaPath",
                table: "ParserConfigs");

            migrationBuilder.DropColumn(
                name: "JsonPricePath",
                table: "ParserConfigs");

            migrationBuilder.DropColumn(
                name: "JsonSoldValue",
                table: "ParserConfigs");

            migrationBuilder.DropColumn(
                name: "JsonStatusPath",
                table: "ParserConfigs");

            migrationBuilder.DropColumn(
                name: "JsonTitlePath",
                table: "ParserConfigs");

            migrationBuilder.DropColumn(
                name: "JsonUnderOptionValue",
                table: "ParserConfigs");

            migrationBuilder.DropColumn(
                name: "JsonUrlPath",
                table: "ParserConfigs");

            migrationBuilder.DropColumn(
                name: "JsonZipCodePath",
                table: "ParserConfigs");

            migrationBuilder.DropColumn(
                name: "ApiListingUrl",
                table: "Agencies");

            migrationBuilder.DropColumn(
                name: "DataSourceType",
                table: "Agencies");
        }
    }
}
