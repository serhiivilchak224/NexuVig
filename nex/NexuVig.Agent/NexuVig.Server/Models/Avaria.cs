namespace NexuVig.Server.Models;

// Avaria (ocorrência) num computador.
public class Avaria
{
    public int Id { get; set; }
    public int ComputadorId { get; set; }
    public Computador Computador { get; set; } = null!;

    public string Titulo { get; set; } = "";
    public string Descricao { get; set; } = "";
    public string Prioridade { get; set; } = "";
    public string Estado { get; set; } = "";
    public string Autor { get; set; } = "";
    public DateTime CriadaEm { get; set; }
    public DateTime? ResolvidaEm { get; set; }

    public List<Intervencao> Intervencoes { get; set; } = new();
}
