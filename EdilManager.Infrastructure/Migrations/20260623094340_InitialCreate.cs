using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EdilManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cantieri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CUP = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cantieri", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Dipendenti",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Cognome = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CodiceFiscale = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    DataNascita = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DataAssunzione = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Mansione = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Livello = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Telefono = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dipendenti", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Fornitori",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PartitaIva = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CodiceFiscale = table.Column<string>(type: "TEXT", nullable: true),
                    Denominazione = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Indirizzo = table.Column<string>(type: "TEXT", nullable: true),
                    Comune = table.Column<string>(type: "TEXT", nullable: true),
                    Provincia = table.Column<string>(type: "TEXT", nullable: true),
                    Cap = table.Column<string>(type: "TEXT", nullable: true),
                    Email = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fornitori", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Presenze",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DipendenteId = table.Column<int>(type: "INTEGER", nullable: false),
                    CantiereId = table.Column<int>(type: "INTEGER", nullable: true),
                    Data = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    OreOrdinarie = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    OreStatordinarie = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Presenze", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Presenze_Cantieri_CantiereId",
                        column: x => x.CantiereId,
                        principalTable: "Cantieri",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Presenze_Dipendenti_DipendenteId",
                        column: x => x.DipendenteId,
                        principalTable: "Dipendenti",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Fatture",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NumeroDocumento = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DataDocumento = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    TipoDocumento = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    TotaleImponibile = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotaleImposta = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotaleDocumento = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ModalitaPagamento = table.Column<string>(type: "TEXT", nullable: true),
                    DataScadenzaPagamento = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    ImportoPagamento = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    FornitoreId = table.Column<int>(type: "INTEGER", nullable: false),
                    CantiereId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fatture", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Fatture_Cantieri_CantiereId",
                        column: x => x.CantiereId,
                        principalTable: "Cantieri",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Fatture_Fornitori_FornitoreId",
                        column: x => x.FornitoreId,
                        principalTable: "Fornitori",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RigheFattura",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FatturaId = table.Column<int>(type: "INTEGER", nullable: false),
                    CodiceArticolo = table.Column<string>(type: "TEXT", nullable: true),
                    Descrizione = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Quantita = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    PrezzoUnitario = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    UnitaMisura = table.Column<string>(type: "TEXT", nullable: true),
                    Sconto = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: true),
                    AliquotaIVA = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    PrezzoTotale = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RigheFattura", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RigheFattura_Fatture_FatturaId",
                        column: x => x.FatturaId,
                        principalTable: "Fatture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cantieri_CUP",
                table: "Cantieri",
                column: "CUP",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dipendenti_CodiceFiscale",
                table: "Dipendenti",
                column: "CodiceFiscale",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Fatture_CantiereId",
                table: "Fatture",
                column: "CantiereId");

            migrationBuilder.CreateIndex(
                name: "IX_Fatture_FornitoreId_NumeroDocumento_DataDocumento",
                table: "Fatture",
                columns: new[] { "FornitoreId", "NumeroDocumento", "DataDocumento" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Fornitori_PartitaIva",
                table: "Fornitori",
                column: "PartitaIva",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Presenze_CantiereId",
                table: "Presenze",
                column: "CantiereId");

            migrationBuilder.CreateIndex(
                name: "IX_Presenze_DipendenteId_Data",
                table: "Presenze",
                columns: new[] { "DipendenteId", "Data" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RigheFattura_FatturaId",
                table: "RigheFattura",
                column: "FatturaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Presenze");

            migrationBuilder.DropTable(
                name: "RigheFattura");

            migrationBuilder.DropTable(
                name: "Dipendenti");

            migrationBuilder.DropTable(
                name: "Fatture");

            migrationBuilder.DropTable(
                name: "Cantieri");

            migrationBuilder.DropTable(
                name: "Fornitori");
        }
    }
}
