using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Immo.Data.Migrations
{
    /// <inheritdoc />
    public partial class addparserconfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParserConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AgencyId = table.Column<int>(type: "INTEGER", nullable: false),
                    TitleSelector = table.Column<string>(type: "TEXT", nullable: true),
                    PriceSelector = table.Column<string>(type: "TEXT", nullable: true),
                    DescriptionSelector = table.Column<string>(type: "TEXT", nullable: true),
                    ImageSelector = table.Column<string>(type: "TEXT", nullable: true),
                    AddressSelector = table.Column<string>(type: "TEXT", nullable: true),
                    ExternalIdPattern = table.Column<string>(type: "TEXT", nullable: true),
                    SpecContainerSelector = table.Column<string>(type: "TEXT", nullable: true),
                    SpecLabelSelector = table.Column<string>(type: "TEXT", nullable: true),
                    SpecValueSelector = table.Column<string>(type: "TEXT", nullable: true),
                    BedroomLabel = table.Column<string>(type: "TEXT", nullable: true),
                    LivingAreaLabel = table.Column<string>(type: "TEXT", nullable: true),
                    PlotAreaLabel = table.Column<string>(type: "TEXT", nullable: true),
                    EpcLabel = table.Column<string>(type: "TEXT", nullable: true),
                    ReferenceLabel = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParserConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParserConfigs_Agencies_AgencyId",
                        column: x => x.AgencyId,
                        principalTable: "Agencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParserConfigs_AgencyId",
                table: "ParserConfigs",
                column: "AgencyId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParserConfigs");
        }
    }
}
