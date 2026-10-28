using System;
using System.Collections.Generic;

namespace NexuVig.Shared
{
    public class DadosEquipamento
    {
        public string AgentId { get; set; } = "";
        public string Nome { get; set; } = "";
        public string SistemaOperativo { get; set; } = "";
        public string Processador { get; set; } = "";

        public List<string> EnderecosIPv4 { get; set; } = new();

        public DateTimeOffset DataHora { get; set; }

        public double UtilizacaoCPU { get; set; }

        public double MemoriaTotalGiB { get; set; }
        public double MemoriaDisponivelGiB { get; set; }
        public double UtilizacaoRAM { get; set; }

        public string UnidadeDisco { get; set; } = "";
        public double DiscoTotalGiB { get; set; }
        public double DiscoLivreGiB { get; set; }

        public long TempoLigadoSegundos { get; set; }
    }
}