using System;
using System.IO;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NexuVig.Shared;

namespace NexuVig.Agent
{
    public class Worker(
        ILogger<Worker> logger,
        IConfiguration configuracao) : BackgroundService
    {
        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            if (!OperatingSystem.IsWindows())
            {
                logger.LogError("Este Agent necessita de Windows.");
                return;
            }

            string? endereco =
                configuracao["NexuVig:EnderecoServer"];

            if (!Uri.TryCreate(
                    endereco,
                    UriKind.Absolute,
                    out var enderecoServer) ||
                enderecoServer.Scheme != Uri.UriSchemeHttps)
            {
                logger.LogError(
                    "Configura um endereço HTTPS válido em " +
                    "NexuVig:EnderecoServer no appsettings.json.");

                return;
            }

            var enderecoMedicoes = new Uri(
                enderecoServer, "/api/medicoes");

            string agentId;

            try
            {
                agentId = ObterAgentId();
            }
            catch (Exception erro)
            {
                logger.LogError(
                    erro,
                    "Não foi possível obter o identificador do Agent.");

                return;
            }

            using var cliente = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };

            var opcoesJSON = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            logger.LogInformation(
                "Agent {AgentId} iniciado. Destino: {Endereco}",
                agentId,
                enderecoMedicoes);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    DadosEquipamento dados = RecolherDados(agentId);

                    string json = JsonSerializer.Serialize(
                        dados, opcoesJSON);

                    logger.LogInformation(
                        "Dados recolhidos:\n{Json}", json);

                    await EnviarDadosAsync(
                        cliente,
                        enderecoMedicoes,
                        dados,
                        stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (OperationCanceledException)
                {
                    logger.LogWarning(
                        "O Server demorou demasiado a responder. " +
                        "Nova tentativa no próximo ciclo.");
                }
                catch (HttpRequestException erro)
                {
                    logger.LogError(
                        erro,
                        "Não foi possível comunicar com o Server. " +
                        "Nova tentativa no próximo ciclo.");
                }
                catch (Exception erro)
                {
                    logger.LogError(
                        erro,
                        "Ocorreu um erro na recolha ou no envio. " +
                        "Nova tentativa no próximo ciclo.");
                }

                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(30),
                        stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }

            logger.LogInformation("Agent terminado.");
        }

        private async Task EnviarDadosAsync(
            HttpClient cliente,
            Uri endereco,
            DadosEquipamento dados,
            CancellationToken stoppingToken)
        {
            using var resposta = await cliente.PostAsJsonAsync(
                endereco,
                dados,
                stoppingToken);

            if (resposta.IsSuccessStatusCode)
            {
                logger.LogInformation(
                    "Medição enviada com sucesso. " +
                    "Computador: {Nome} | Estado HTTP: {Estado}",
                    dados.Nome,
                    (int)resposta.StatusCode);

                return;
            }

            string detalhe = await resposta.Content.ReadAsStringAsync(
                stoppingToken);

            logger.LogWarning(
                "O Server não aceitou a medição. " +
                "Estado HTTP: {Estado} | Resposta: {Detalhe}",
                (int)resposta.StatusCode,
                detalhe);
        }

        private string ObterAgentId()
        {
            string pasta = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "NexuVig");

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
        private DadosEquipamento RecolherDados(string agentId)
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

        private void RecolherRede(DadosEquipamento dados)
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
        private void RecolherProcessador(DadosEquipamento dados)
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
        private void RecolherCPU(DadosEquipamento dados)
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
        private void RecolherMemoria(DadosEquipamento dados)
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