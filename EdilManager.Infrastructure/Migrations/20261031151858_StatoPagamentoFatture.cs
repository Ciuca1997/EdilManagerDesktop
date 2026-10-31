using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EdilManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StatoPagamentoFatture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "DataPagamento",
                table: "Fatture",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Stato",
                table: "Fatture",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataPagamento",
                table: "Fatture");

            migrationBuilder.DropColumn(
                name: "Stato",
                table: "Fatture");
        }
    }
}
