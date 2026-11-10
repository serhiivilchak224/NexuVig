using NexuVig.Shared;

namespace NexuVig.Dashboard;

// Regras usadas pelo Painel e pela página de Detalhes (assim ficam iguais nas duas).
public static class Monitorizacao
{
    // O Agent envia a cada 30 s: sem notícias há mais de 90 s, está sem comunicação.
    public const int SegundosSemComunicacao = 90;

    public const double LimiteAviso = 75;
    public const double LimitePerigo = 90;
    public const double LimiteDiscoLivre = 15;

    public static bool EstaOnline(DadosEquipamento pc) =>
        (DateTimeOffset.UtcNow - pc.DataHora).TotalSeconds < SegundosSemComunicacao;

    public static double PercentagemDiscoLivre(DadosEquipamento pc) =>
        pc.DiscoTotalGiB > 0 ? pc.DiscoLivreGiB / pc.DiscoTotalGiB * 100 : 100;

    public static bool CpuAlta(DadosEquipamento pc) => pc.UtilizacaoCPU > LimitePerigo;
    public static bool RamAlta(DadosEquipamento pc) => pc.UtilizacaoRAM > LimitePerigo;
    public static bool DiscoCheio(DadosEquipamento pc) => PercentagemDiscoLivre(pc) < LimiteDiscoLivre;

    public static bool TemAlerta(DadosEquipamento pc) => CpuAlta(pc) || RamAlta(pc) || DiscoCheio(pc);

    // Cor da barra conforme a utilização (CPU, RAM ou disco usado).
    public static string ClasseBarra(double percentagemUso) =>
        percentagemUso > LimitePerigo ? "nv-barra-perigo" :
        percentagemUso > LimiteAviso ? "nv-barra-aviso" :
        "nv-barra-normal";

    public static string TempoDesde(DateTimeOffset momento)
    {
        var passado = DateTimeOffset.UtcNow - momento;

        if (passado.TotalSeconds < 5) return "agora mesmo";
        if (passado.TotalSeconds < 60) return $"há {(int)passado.TotalSeconds} s";
        if (passado.TotalMinutes < 60) return $"há {(int)passado.TotalMinutes} min";
        if (passado.TotalHours < 24) return $"há {(int)passado.TotalHours} h";

        int dias = (int)passado.TotalDays;
        return dias == 1 ? "há 1 dia" : $"há {dias} dias";
    }

    // Prefere o IP da rede local (192.168.x, 10.x, 172.16-31.x) a IPs de VPN.
    public static string? IpPrincipal(DadosEquipamento pc)
    {
        if (pc.EnderecosIPv4 is not { Count: > 0 } ips) return null;

        return ips.FirstOrDefault(EIpLocal) ?? ips[0];
    }

    private static bool EIpLocal(string ip)
    {
        if (ip.StartsWith("192.168.") || ip.StartsWith("10.")) return true;

        var partes = ip.Split('.');
        return partes.Length == 4 && partes[0] == "172" &&
               int.TryParse(partes[1], out int segundo) && segundo is >= 16 and <= 31;
    }
}
