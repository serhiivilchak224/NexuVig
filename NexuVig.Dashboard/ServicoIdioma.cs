namespace NexuVig.Dashboard;

public class ServicoIdioma
{
    public string IdiomaAtual { get; private set; } = "PT";

    public event Action? OnChange;

    public void MudarIdioma(string novoIdioma)
    {
        IdiomaAtual = novoIdioma;
        OnChange?.Invoke();
    }

    public string Traduzir(string chave)
    {
        if (IdiomaAtual == "EN")
        {
            return chave switch
            {
                "Titulo" => "Monitoring Panel",
                "ServidorOnline" => "Server Online",
                "TotalEquipamentos" => "Total Devices",
                "Online" => "Online",
                "SemComunicacao" => "No Communication",
                "AlertasAtivos" => "Active Alerts",
                "EquipamentosRecentes" => "Recent Devices",
                "Equipamento" => "Device",
                "Estado" => "Status",
                "CPU" => "CPU",
                "RAM" => "RAM",
                "UltimaComunicacao" => "Last Communication",
                "SemDados" => "No data yet. Make sure Server and Agent are running.",
                _ => chave
            };
        }

        // Português (Padrão)
        return chave switch
        {
            "Titulo" => "Painel de Monitorização",
            "ServidorOnline" => "Servidor Online",
            "TotalEquipamentos" => "Total de Equipamentos",
            "Online" => "Online",
            "SemComunicacao" => "Sem Comunicação",
            "AlertasAtivos" => "Alertas Ativos",
            "EquipamentosRecentes" => "Equipamentos Recentes",
            "Equipamento" => "Equipamento",
            "Estado" => "Estado",
            "CPU" => "CPU",
            "RAM" => "RAM",
            "UltimaComunicacao" => "Última Comunicação",
            "SemDados" => "Ainda não existem dados. Certifica-te que o Server e o Agent estão a correr.",
            _ => chave
        };
    }
}