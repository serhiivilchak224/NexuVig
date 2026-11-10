using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexuVig.Server.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarIPsEDisco : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "EnderecosIPv4",
                table: "Computadores",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]");

            migrationBuilder.AddColumn<string>(
                name: "UnidadeDisco",
                table: "Computadores",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnderecosIPv4",
                table: "Computadores");

            migrationBuilder.DropColumn(
                name: "UnidadeDisco",
                table: "Computadores");
        }
    }
}
