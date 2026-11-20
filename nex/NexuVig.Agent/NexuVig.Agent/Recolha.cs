using System;
using System.IO;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using NexuVig.Shared;

namespace NexuVig.Agent
{
    // Recolha dos dados do computador (CPU, RAM, disco, rede).
    // Este ficheiro é usado pelo Agent (serviço) e pelo programa NexuVig (janela),
    // para os dois mostrarem exatamente os mesmos valores.
    public static class Recolha
    {
        // Pasta comum (C:\ProgramData\NexuVig): igual para o Serviço do Windows
        // e para execução normal.
        public static string PastaDados => Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.CommonApplicationData),
            "NexuVig");

        public static string ObterAgentId()
        {
            // O ID fica em C:\ProgramData\NexuVig e não muda entre
            // reinstalações (o computador não aparece duplicado).
            string pasta = PastaDados;

            Directory.CreateDirectory(pasta);

            string ficheiro = Path.Combine(pasta, "agent-id.txt");

            if (File.Exists(ficheiro))
            {
                string valor = File.ReadAllText(ficheiro).Trim();

                if (!Guid.TryParse(valor, out Guid id) ||
                    id == Guid.Empty)
                {
                    throw new InvalidDataException(
                        "O ficheiro agent-id.txt contém " +
                        "um identificador inválido.");
                }

                return id.ToString();
            }

            string novoId = Guid.NewGuid().ToString();

            File.WriteAllText(ficheiro, novoId);

            return novoId;
        }

        [SupportedOSPlatform("windows")]
        public static DadosEquipamento Recolher(string agentId)
        {
            var dados = new DadosEquipamento
            {
                AgentId = agentId,
                Nome = Environment.MachineName,
                SistemaOperativo = RuntimeInformation.OSDescription,
                DataHora = DateTimeOffset.UtcNow,
                TempoLigadoSegundos = Environment.TickCount64 / 1000
            };

            string raiz = Path.GetPathRoot(
                Environment.SystemDirectory)!;

            DriveInfo disco = new DriveInfo(raiz);

            dados.UnidadeDisco = disco.Name;

            dados.DiscoTotalGiB =
                disco.TotalSize / Math.Pow(1024, 3);

            dados.DiscoLivreGiB =
                disco.AvailableFreeSpace / Math.Pow(1024, 3);

            RecolherRede(dados);
            RecolherProcessador(dados);
            RecolherCPU(dados);
            RecolherMemoria(dados);

            return dados;
        }

        private static void RecolherRede(DadosEquipamento dados)
        {
            foreach (NetworkInterface placa
                in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (placa.OperationalStatus != OperationalStatus.Up ||
                    placa.NetworkInterfaceType ==
                        NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                foreach (UnicastIPAddressInformation endereco
                    in placa.GetIPProperties().UnicastAddresses)
                {
                    if (endereco.Address.AddressFamily !=
                            AddressFamily.InterNetwork ||
                        IPAddress.IsLoopback(endereco.Address))
                    {
                        continue;
                    }

                    string ip = endereco.Address.ToString();

                    if (!dados.EnderecosIPv4.Contains(ip))
                    {
                        dados.EnderecosIPv4.Add(ip);
                    }
                }
            }
        }

        [SupportedOSPlatform("windows")]
        private static void RecolherProcessador(DadosEquipamento dados)
        {
            using var pesquisa = new ManagementObjectSearcher(
                "SELECT Name FROM Win32_Processor");

            using var resultados = pesquisa.Get();

            foreach (ManagementObject processador in resultados)
            {
                using (processador)
                {
                    dados.Processador =
                        processador["Name"]?.ToString()?.Trim()
                        ?? "Desconhecido";

                    return;
                }
            }

            throw new InvalidOperationException(
                "Não foi possível identificar o processador.");
        }

        [SupportedOSPlatform("windows")]
        private static void RecolherCPU(DadosEquipamento dados)
        {
            using var pesquisa = new ManagementObjectSearcher(
                "SELECT PercentProcessorTime " +
                "FROM Win32_PerfFormattedData_PerfOS_Processor " +
                "WHERE Name = '_Total'");

            using var resultados = pesquisa.Get();

            foreach (ManagementObject cpu in resultados)
            {
                using (cpu)
                {
                    if (cpu["PercentProcessorTime"] is null)
                    {
                        throw new InvalidOperationException(
                            "O contador da CPU não devolveu um valor.");
                    }

                    dados.UtilizacaoCPU = Convert.ToDouble(
                        cpu["PercentProcessorTime"]);

                    return;
                }
            }

            throw new InvalidOperationException(
                "O contador da CPU não está disponível.");
        }

        [SupportedOSPlatform("windows")]
        private static void RecolherMemoria(DadosEquipamento dados)
        {
            using var pesquisa = new ManagementObjectSearcher(
                "SELECT TotalVisibleMemorySize, FreePhysicalMemory " +
                "FROM Win32_OperatingSystem");

            using var resultados = pesquisa.Get();

            foreach (ManagementObject sistema in resultados)
            {
                using (sistema)
                {
                    double totalKB = Convert.ToDouble(
                        sistema["TotalVisibleMemorySize"]);

                    double livreKB = Convert.ToDouble(
                        sistema["FreePhysicalMemory"]);

                    if (totalKB <= 0 ||
                        livreKB < 0 ||
                        livreKB > totalKB)
                    {
                        throw new InvalidOperationException(
                            "O Windows devolveu valores de RAM inválidos.");
                    }

                    dados.MemoriaTotalGiB =
                        totalKB / (1024 * 1024);

                    dados.MemoriaDisponivelGiB =
                        livreKB / (1024 * 1024);

                    dados.UtilizacaoRAM =
                        (totalKB - livreKB) / totalKB * 100;

                    return;
                }
            }

            throw new InvalidOperationException(
                "Não foi possível recolher a memória RAM.");
        }
    }
}
