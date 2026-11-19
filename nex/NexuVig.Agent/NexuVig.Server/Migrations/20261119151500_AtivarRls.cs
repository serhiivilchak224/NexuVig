using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexuVig.Server.Migrations
{
    /// <summary>
    /// Ativa Row Level Security (RLS) em todas as tabelas.
    /// O Supabase disponibiliza as tabelas do esquema "public" numa API própria (Data API);
    /// com RLS ativo e sem políticas, essa API não consegue ler nem escrever nada.
    /// O Server liga-se como dono das tabelas (postgres), que não é afetado pelo RLS.
    /// </summary>
    public partial class AtivarRls : Migration
    {
        private static readonly string[] Tabelas =
            ["Computadores", "Medicoes", "Utilizadores", "Avarias", "Intervencoes", "__EFMigrationsHistory"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (string tabela in Tabelas)
                migrationBuilder.Sql($"ALTER TABLE public.\"{tabela}\" ENABLE ROW LEVEL SECURITY;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (string tabela in Tabelas)
                migrationBuilder.Sql($"ALTER TABLE public.\"{tabela}\" DISABLE ROW LEVEL SECURITY;");
        }
    }
}
