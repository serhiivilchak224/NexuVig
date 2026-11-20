using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
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
        // Token deste computador (registo automático). Fica numa pasta que só
        // o sistema e os administradores conseguem ler (criada pelo instalador).
        private static string FicheiroToken => Path.Combine(
            Recolha.PastaDados, "seguro", "agent-token.txt");

        // Estado da ligação, lido pelo programa NexuVig (janela).
        private static string FicheiroEstado => Path.Combine(
            Recolha.PastaDados, "estado.txt");

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

                EscreverEstado("Endereço do Server inválido");
                return;
            }

            var enderecoMedicoes = new Uri(
                enderecoServer, "/api/medicoes");

            var enderecoRegisto = new Uri(
                enderecoServer, "/api/registo");

            string agentId;

            try
            {
                agentId = Recolha.ObterAgentId();
            }
            catch (Exception erro)
            {
                logger.LogError(
                    erro,
                    "Não foi possível obter o identificador do Agent.");

                return;
            }

            // Dois modos:
            // - com NexuVig:ChaveApi (chave partilhada, instalada pelo administrador);
            // - sem chave: o Agent regista-se sozinho e recebe um token próprio,
            //   ficando à espera de aprovação no Dashboard.
            string? chaveApi = configuracao["NexuVig:ChaveApi"];
            bool modoChave = !string.IsNullOrWhiteSpace(chaveApi);
            string? token = modoChave ? null : LerToken();
            DateTime proximoRegisto = DateTime.MinValue;

            using var cliente = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60)
            };

            var opcoesJSON = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            logger.LogInformation(
                "Agent {AgentId} iniciado ({Modo}). Destino: {Endereco}",
                agentId,
                modoChave ? "chave partilhada" : "registo automático",
                enderecoMedicoes);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    DadosEquipamento dados = Recolha.Recolher(agentId);

                    string json = JsonSerializer.Serialize(
                        dados, opcoesJSON);

                    // Debug: aparece na consola em desenvolvimento, mas não
                    // enche o Visualizador de Eventos a cada 30 segundos.
                    logger.LogDebug(
                        "Dados recolhidos:\n{Json}", json);

                    if (!modoChave && token is null &&
                        DateTime.UtcNow >= proximoRegisto)
                    {
                        token = await RegistarAsync(
                            cliente,
                            enderecoRegisto,
                            dados,
                            stoppingToken);

                        // Se falhar, só volta a tentar passados 10 minutos.
                        if (token is null)
                        {
                            proximoRegisto =
                                DateTime.UtcNow.AddMinutes(10);
                        }
                    }

                    if (modoChave || token is not null)
                    {
                        bool tokenRecusado = await EnviarDadosAsync(
                            cliente,
                            enderecoMedicoes,
                            dados,
                            modoChave ? "X-Api-Key" : "X-Agent-Token",
                            modoChave ? chaveApi! : token!,
                            stoppingToken);

                        // O Server já não conhece este token (por exemplo, o
                        // administrador removeu o computador): regista-se de novo.
                        if (tokenRecusado && !modoChave)
                        {
                            ApagarToken();
                            token = null;
                            proximoRegisto =
                                DateTime.UtcNow.AddMinutes(10);
                        }
                    }
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

                    EscreverEstado("Sem ligação ao Server");
                }
                catch (HttpRequestException erro)
                {
                    logger.LogError(
                        erro,
                        "Não foi possível comunicar com o Server. " +
                        "Nova tentativa no próximo ciclo.");

                    EscreverEstado("Sem ligação ao Server");
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

        // Pede ao Server um token para este computador. Devolve null se falhar.
        private async Task<string?> RegistarAsync(
            HttpClient cliente,
            Uri endereco,
            DadosEquipamento dados,
            CancellationToken stoppingToken)
        {
            var pedido = new PedidoRegisto
            {
                AgentId = dados.AgentId,
                Nome = dados.Nome
            };

            using var resposta = await cliente.PostAsJsonAsync(
                endereco,
                pedido,
                stoppingToken);

            if (!resposta.IsSuccessStatusCode)
            {
                string detalhe = await resposta.Content.ReadAsStringAsync(
                    stoppingToken);

                logger.LogWarning(
                    "O Server não aceitou o registo. " +
                    "Estado HTTP: {Estado} | Resposta: {Detalhe}",
                    (int)resposta.StatusCode,
                    detalhe);

                EscreverEstado(
                    resposta.StatusCode == HttpStatusCode.Conflict
                        ? "Já registado: pede ao administrador para remover este computador no painel"
                        : "Registo recusado pelo Server");

                return null;
            }

            RespostaRegisto? registo =
                await resposta.Content.ReadFromJsonAsync<RespostaRegisto>(
                    stoppingToken);

            if (string.IsNullOrWhiteSpace(registo?.Token))
            {
                logger.LogWarning(
                    "O Server respondeu ao registo sem token.");

                return null;
            }

            GuardarToken(registo.Token);

            logger.LogInformation(
                "Computador registado no Server. " +
                "Fica à espera de aprovação do administrador.");

            return registo.Token;
        }

        // Devolve true quando o Server recusou as credenciais (401).
        private async Task<bool> EnviarDadosAsync(
            HttpClient cliente,
            Uri endereco,
            DadosEquipamento dados,
            string cabecalho,
            string segredo,
            CancellationToken stoppingToken)
        {
            using var pedido = new HttpRequestMessage(
                HttpMethod.Post, endereco)
            {
                Content = JsonContent.Create(dados)
            };

            pedido.Headers.Add(cabecalho, segredo);

            using var resposta = await cliente.SendAsync(
                pedido,
                stoppingToken);

            if (resposta.StatusCode == HttpStatusCode.Accepted)
            {
                // 202: registado, mas ainda não aprovado no Dashboard.
                logger.LogDebug(
                    "Computador à espera de aprovação do administrador.");

                EscreverEstado("À espera de aprovação do administrador");
                return false;
            }

            if (resposta.IsSuccessStatusCode)
            {
                logger.LogInformation(
                    "Medição enviada com sucesso. " +
                    "Computador: {Nome} | Estado HTTP: {Estado}",
                    dados.Nome,
                    (int)resposta.StatusCode);

                EscreverEstado("Ligado ao Server");
                return false;
            }

            string detalhe = await resposta.Content.ReadAsStringAsync(
                stoppingToken);

            logger.LogWarning(
                "O Server não aceitou a medição. " +
                "Estado HTTP: {Estado} | Resposta: {Detalhe}",
                (int)resposta.StatusCode,
                detalhe);

            EscreverEstado(
                $"O Server recusou os dados (HTTP {(int)resposta.StatusCode})");

            return resposta.StatusCode == HttpStatusCode.Unauthorized;
        }

        private string? LerToken()
        {
            try
            {
                if (!File.Exists(FicheiroToken))
                {
                    return null;
                }

                string valor = File.ReadAllText(FicheiroToken).Trim();
                return valor.Length == 0 ? null : valor;
            }
            catch (Exception erro)
            {
                logger.LogWarning(
                    erro, "Não foi possível ler o token do Agent.");

                return null;
            }
        }

        private void GuardarToken(string token)
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(FicheiroToken)!);

            File.WriteAllText(FicheiroToken, token);
        }

        private void ApagarToken()
        {
            try
            {
                File.Delete(FicheiroToken);
            }
            catch (Exception erro)
            {
                logger.LogWarning(
                    erro, "Não foi possível apagar o token do Agent.");
            }
        }

        // Uma linha: estado | data e hora (UTC). Se falhar, o Agent continua.
        private void EscreverEstado(string estado)
        {
            try
            {
                Directory.CreateDirectory(Recolha.PastaDados);

                File.WriteAllText(
                    FicheiroEstado,
                    $"{estado}|{DateTimeOffset.UtcNow:O}");
            }
            catch (Exception erro)
            {
                logger.LogDebug(
                    erro, "Não foi possível escrever o estado.");
            }
        }
    }
}
