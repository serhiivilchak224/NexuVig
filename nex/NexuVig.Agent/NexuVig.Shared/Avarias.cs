using System;
using System.Collections.Generic;

namespace NexuVig.Shared
{
    // Estados de uma avaria (guardados sem acentos; Nome() dá o texto para mostrar).
    public static class EstadosAvaria
    {
        public const string Aberta = "Aberta";
        public const string EmCurso = "EmCurso";
        public const string Resolvida = "Resolvida";

        public static bool Valido(string? estado) =>
            estado == Aberta || estado == EmCurso || estado == Resolvida;

        public static string Nome(string? estado) => estado switch
        {
            EmCurso => "Em curso",
            _ => estado ?? ""
        };
    }

    public static class Prioridades
    {
        public const string Baixa = "Baixa";
        public const string Media = "Media";
        public const string Alta = "Alta";

        public static bool Valida(string? prioridade) =>
            prioridade == Baixa || prioridade == Media || prioridade == Alta;

        public static string Nome(string? prioridade) => prioridade switch
        {
            Media => "Média",
            _ => prioridade ?? ""
        };

        // Para ordenar: Alta primeiro.
        public static int Ordem(string? prioridade) => prioridade switch
        {
            Alta => 0,
            Media => 1,
            _ => 2
        };
    }

    // Linha da lista de avarias.
    public class AvariaResumo
    {
        public int Id { get; set; }
        public string AgentId { get; set; } = "";
        public string Computador { get; set; } = "";
        public string Titulo { get; set; } = "";
        public string Prioridade { get; set; } = "";
        public string Estado { get; set; } = "";
        public string Autor { get; set; } = "";
        public DateTimeOffset CriadaEm { get; set; }
        public DateTimeOffset? ResolvidaEm { get; set; }
        public int NumeroIntervencoes { get; set; }
        public DateTimeOffset UltimaAtualizacao { get; set; }
    }

    // Avaria completa, com a descrição e o histórico de intervenções.
    public class AvariaDetalhe : AvariaResumo
    {
        public string Descricao { get; set; } = "";
        public List<IntervencaoInfo> Intervencoes { get; set; } = new();
    }

    public class IntervencaoInfo
    {
        public int Id { get; set; }
        public DateTimeOffset Data { get; set; }
        public string Descricao { get; set; } = "";
        public string Autor { get; set; } = "";
    }

    // Dashboard -> Server: registar uma avaria.
    public class NovaAvaria
    {
        public string AgentId { get; set; } = "";
        public string Titulo { get; set; } = "";
        public string Descricao { get; set; } = "";
        public string Prioridade { get; set; } = Prioridades.Media;
        public string Autor { get; set; } = "";
    }

    // Dashboard -> Server: registar uma intervenção (e, opcionalmente, mudar o estado da avaria).
    public class NovaIntervencao
    {
        public string Descricao { get; set; } = "";
        public string Autor { get; set; } = "";
        public string? NovoEstado { get; set; }
    }
}
