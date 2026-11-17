using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace NexuVig.Shared
{
    // Perfis de utilizador do Dashboard.
    public static class Perfis
    {
        // Tudo: aprovar, rejeitar e remover computadores, gerir utilizadores.
        public const string Administrador = "Administrador";

        // Consulta: vê o painel e os detalhes (por exemplo, um professor ou técnico).
        public const string Tecnico = "Tecnico";

        public static bool Valido(string? perfil) =>
            perfil == Administrador || perfil == Tecnico;

        public static string Nome(string? perfil) =>
            perfil == Tecnico ? "Técnico" : perfil ?? "";
    }

    // Nome de utilizador simplificado: "João Silva" -> "joao.silva".
    // Usado ao criar a conta e ao entrar, para acentos, maiúsculas e espaços não importarem.
    public static class NomesUtilizador
    {
        public static string Normalizar(string? texto)
        {
            string semAcentos = new string(
                (texto ?? "").Trim().ToLowerInvariant()
                    .Normalize(NormalizationForm.FormD)
                    .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    .ToArray());

            var resultado = new StringBuilder();
            foreach (char c in semAcentos)
            {
                if (char.IsWhiteSpace(c))
                {
                    if (resultado.Length > 0 && resultado[^1] != '.') resultado.Append('.');
                }
                else if (char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_')
                {
                    resultado.Append(c);
                }
            }

            return resultado.ToString().Trim('.');
        }
    }

    // Dashboard -> Server: confirmar utilizador e palavra-passe.
    public class PedidoAutenticacao
    {
        public string Utilizador { get; set; } = "";
        public string Password { get; set; } = "";
    }

    // Dados de uma conta (nunca inclui a palavra-passe).
    public class UtilizadorInfo
    {
        public string Utilizador { get; set; } = "";
        public string Nome { get; set; } = "";
        public string Perfil { get; set; } = "";
        public DateTimeOffset CriadoEm { get; set; }
    }

    // Dashboard -> Server: criar uma conta.
    public class NovoUtilizador
    {
        public string Utilizador { get; set; } = "";
        public string Nome { get; set; } = "";
        public string Password { get; set; } = "";
        public string Perfil { get; set; } = Perfis.Tecnico;
    }
}
