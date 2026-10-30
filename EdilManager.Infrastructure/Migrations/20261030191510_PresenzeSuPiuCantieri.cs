using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EdilManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PresenzeSuPiuCantieri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Presenze_DipendenteId_Data",
                table: "Presenze");

            migrationBuilder.CreateIndex(
                name: "IX_Presenze_DipendenteId_Data",
                table: "Presenze",
                columns: new[] { "DipendenteId", "Data" });

            migrationBuilder.CreateIndex(
                name: "IX_Presenze_DipendenteId_Data_CantiereId",
                table: "Presenze",
                columns: new[] { "DipendenteId", "Data", "CantiereId" },
                unique: true,
                filter: "\"CantiereId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Presenze_DipendenteId_Data",
                table: "Presenze");

            migrationBuilder.DropIndex(
                name: "IX_Presenze_DipendenteId_Data_CantiereId",
                table: "Presenze");

            migrationBuilder.CreateIndex(
                name: "IX_Presenze_DipendenteId_Data",
                table: "Presenze",
                columns: new[] { "DipendenteId", "Data" },
                unique: true);
        }
    }
}
