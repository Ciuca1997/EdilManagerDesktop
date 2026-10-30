using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EdilManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DatiAzienda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Aziende",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RagioneSociale = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PartitaIva = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    CodiceFiscale = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Indirizzo = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Cap = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    Comune = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Provincia = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    Telefono = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Aziende", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Aziende");
        }
    }
}
