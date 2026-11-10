namespace NexuVig.Server.Models;

public class Medicao
{
    public int Id { get; set; }
    public int ComputadorId { get; set; }
    public Computador Computador { get; set; } = null!;

    public DateTime DataHora { get; set; }
    public double UtilizacaoCPU { get; set; }
    public double UtilizacaoRAM { get; set; }
    public double MemoriaTotalGiB { get; set; }
    public double MemoriaDisponivelGiB { get; set; }
    public double DiscoTotalGiB { get; set; }
    public double DiscoLivreGiB { get; set; }
    public long TempoLigadoSegundos { get; set; }
}
