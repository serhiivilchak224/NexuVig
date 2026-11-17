using System;

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
