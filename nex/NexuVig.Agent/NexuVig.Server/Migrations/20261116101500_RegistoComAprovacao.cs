using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexuVig.Server.Migrations
{
    /// <inheritdoc />
    public partial class RegistoComAprovacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Os computadores que já existem ficam aprovados (defaultValue: true).
            migrationBuilder.AddColumn<bool>(
                name: "Aprovado",
                table: "Computadores",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "TokenHash",
                table: "Computadores",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Aprovado",
                table: "Computadores");

            migrationBuilder.DropColumn(
                name: "TokenHash",
                table: "Computadores");
        }
    }
}
