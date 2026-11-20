using NexuVig.Agent;
using NexuVig.Shared;

namespace NexuVig.App;

// Janela principal: mostra os dados deste computador, atualizados a cada 2 segundos,
// e o estado da ligação do serviço "NexuVig Agent" ao Server.
internal sealed class JanelaPrincipal : Form
{
    private static readonly Color CorFundo = Color.FromArgb(15, 23, 42);
    private static readonly Color CorTexto = Color.FromArgb(226, 232, 240);
    private static readonly Color CorSecundaria = Color.FromArgb(148, 163, 184);

    private const int Margem = 24;
    private const int Largura = 440;

    private readonly Label nome;
    private readonly Label sistema;
    private readonly Label processador;
    private readonly Label rede;
    private readonly Label tempoLigado;
    private readonly Label ligacao;
    private readonly BarraUso cpu = new("Processador (CPU)");
    private readonly BarraUso ram = new("Memória (RAM)");
    private readonly BarraUso disco = new("Disco");

    private readonly System.Windows.Forms.Timer temporizador = new() { Interval = 2000 };
    private readonly string agentId = LerAgentId();
    private bool aAtualizar;
    private int y = Margem;

    public JanelaPrincipal()
    {
        Text = "NexuVig";
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = CorFundo;
        ForeColor = CorTexto;
        Font = new Font("Segoe UI", 10F);

        Titulo("NexuVig");
        nome = Linha("A recolher dados...", CorTexto, negrito: true);
        sistema = Linha("", CorSecundaria);
        processador = Linha("", CorSecundaria);
        rede = Linha("", CorSecundaria);
        y += 12;

        Barra(cpu);
        Barra(ram);
        Barra(disco);
        y += 4;

        tempoLigado = Linha("", CorSecundaria);
        ligacao = Linha("", CorTexto, negrito: true);
        Linha("Identificador: " + (agentId.Length > 0 ? agentId : "ainda não criado"), CorSecundaria);

        ClientSize = new Size(Largura + 2 * Margem, y + Margem - 6);

        temporizador.Tick += async (_, _) => await AtualizarAsync();
        Shown += async (_, _) =>
        {
            await AtualizarAsync();
            temporizador.Start();
        };
    }

    private void Titulo(string texto)
    {
        var etiqueta = new Label
        {
            Text = texto,
            Font = new Font("Segoe UI Semibold", 18F),
            AutoSize = false,
            Location = new Point(Margem, y),
            Size = new Size(Largura, 40)
        };
        Controls.Add(etiqueta);
        y += 46;
    }

    private Label Linha(string texto, Color cor, bool negrito = false)
    {
        var etiqueta = new Label
        {
            Text = texto,
            ForeColor = cor,
            Font = negrito ? new Font("Segoe UI Semibold", 10.5F) : Font,
            AutoSize = false,
            AutoEllipsis = true,
            Location = new Point(Margem, y),
            Size = new Size(Largura, 24)
        };
        Controls.Add(etiqueta);
        y += 26;
        return etiqueta;
    }

    private void Barra(BarraUso barra)
    {
        barra.Location = new Point(Margem, y);
        barra.Size = new Size(Largura, 46);
        Controls.Add(barra);
        y += 60;
    }

    private async Task AtualizarAsync()
    {
        if (aAtualizar) return;
        aAtualizar = true;

        try
        {
            // A recolha usa WMI e pode demorar: corre fora da janela para não a bloquear.
            DadosEquipamento dados = await Task.Run(() => Recolha.Recolher(agentId));
            Mostrar(dados);
        }
        catch (Exception erro)
        {
            nome.Text = "Não foi possível recolher os dados: " + erro.Message;
        }
        finally
        {
            aAtualizar = false;
        }

        ligacao.Text = "Ligação: " + LerEstado();
    }

    private void Mostrar(DadosEquipamento dados)
    {
        nome.Text = dados.Nome;
        sistema.Text = dados.SistemaOperativo;
        processador.Text = dados.Processador;
        rede.Text = dados.EnderecosIPv4.Count > 0
            ? "IP: " + string.Join(", ", dados.EnderecosIPv4)
            : "Sem rede";

        cpu.Definir(dados.UtilizacaoCPU, $"{dados.UtilizacaoCPU:F0} %");

        double ramUsada = dados.MemoriaTotalGiB - dados.MemoriaDisponivelGiB;
        ram.Definir(dados.UtilizacaoRAM,
            $"{ramUsada:F1} de {dados.MemoriaTotalGiB:F1} GiB ({dados.UtilizacaoRAM:F0} %)");

        double discoUsado = dados.DiscoTotalGiB > 0
            ? (dados.DiscoTotalGiB - dados.DiscoLivreGiB) / dados.DiscoTotalGiB * 100
            : 0;
        disco.Definir(discoUsado,
            $"{dados.UnidadeDisco} {dados.DiscoLivreGiB:F1} GiB livres de {dados.DiscoTotalGiB:F1} GiB");

        var ligado = TimeSpan.FromSeconds(dados.TempoLigadoSegundos);
        tempoLigado.Text = ligado.TotalDays >= 1
            ? $"Ligado há {(int)ligado.TotalDays} d {ligado.Hours} h {ligado.Minutes} min"
            : $"Ligado há {ligado.Hours} h {ligado.Minutes} min";
    }

    // O identificador é criado pelo serviço; o programa só o lê.
    private static string LerAgentId()
    {
        try
        {
            string ficheiro = Path.Combine(Recolha.PastaDados, "agent-id.txt");
            return File.Exists(ficheiro) ? File.ReadAllText(ficheiro).Trim() : "";
        }
        catch
        {
            return "";
        }
    }

    // O serviço escreve "estado|data e hora (UTC)" em C:\ProgramData\NexuVig\estado.txt.
    private static string LerEstado()
    {
        try
        {
            string ficheiro = Path.Combine(Recolha.PastaDados, "estado.txt");
            if (!File.Exists(ficheiro))
                return "o serviço ainda não comunicou";

            string[] partes = File.ReadAllText(ficheiro).Split('|');
            string estado = partes[0].Trim();

            if (partes.Length > 1 &&
                DateTimeOffset.TryParse(partes[1], null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var momento) &&
                DateTimeOffset.UtcNow - momento > TimeSpan.FromMinutes(3))
            {
                return estado + " (sem novidades há mais de 3 minutos: o serviço pode estar parado)";
            }

            return estado;
        }
        catch
        {
            return "não foi possível ler o estado do serviço";
        }
    }
}
