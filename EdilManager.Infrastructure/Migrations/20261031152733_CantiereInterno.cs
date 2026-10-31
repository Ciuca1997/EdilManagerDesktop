using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EdilManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CantiereInterno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Interno",
                table: "Cantieri",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Interno",
                table: "Cantieri");
        }
    }
}
