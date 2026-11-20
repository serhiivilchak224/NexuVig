namespace NexuVig.Server.Models;

// O que um técnico fez numa avaria (diagnóstico, reparação, substituição...).
public class Intervencao
{
    public int Id { get; set; }
    public int AvariaId { get; set; }
    public Avaria Avaria { get; set; } = null!;

    public DateTime Data { get; set; }
    public string Descricao { get; set; } = "";
    public string Autor { get; set; } = "";
}
