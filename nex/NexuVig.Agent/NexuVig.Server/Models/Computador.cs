namespace NexuVig.Server.Models;

public class Computador
{
    public int Id { get; set; }
    public string AgentId { get; set; } = "";
    public string Nome { get; set; } = "";
    public string SistemaOperativo { get; set; } = "";
    public string Processador { get; set; } = "";
    public List<string> EnderecosIPv4 { get; set; } = new();
    public string UnidadeDisco { get; set; } = "";
    public DateTime UltimaComunicacao { get; set; }

    public List<Medicao> Medicoes { get; set; } = new();
}
