using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NexuVig.Shared;
using NexuVig.Server.Data;
using NexuVig.Server.Models;

// O ambiente vem de ASPNETCORE_ENVIRONMENT (Development por defeito, para o trabalho local;
// o Dockerfile define Production). User Secrets só são lidos em Development.
var ambiente = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? Environments.Development;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    EnvironmentName = ambiente
});

// Endereço(s) onde o Server ouve:
// - alojamento online (Render): a variável PORT define a porta, ouvindo em HTTP atrás do proxy
//   (o HTTPS é terminado pela plataforma);
// - local: https://localhost:7259, ou NexuVig:EnderecosServer
//   (ex.: --NexuVig:EnderecosServer="https://localhost:7259;https://192.168.56.1:7259").
var porta = Environment.GetEnvironmentVariable("PORT");
var enderecos = builder.Configuration["NexuVig:EnderecosServer"];
if (!string.IsNullOrWhiteSpace(porta))
    builder.WebHost.UseUrls($"http://0.0.0.0:{porta}");
else
    builder.WebHost.UseUrls(string.IsNullOrWhiteSpace(enderecos) ? "https://localhost:7259" : enderecos);

var connectionString = builder.Configuration.GetConnectionString("SupabaseConnection");
builder.Services.AddDbContext<NexuVigContext>(options =>
    options.UseNpgsql(connectionString));

var app = builder.Build();

// Segurança (cabeçalho X-Api-Key, chaves vindas de User Secrets / variáveis de ambiente):
// - POST /api/medicoes (Agent)        -> NexuVig:ChaveApi
// - GET  /api/medicoes (Dashboard)    -> NexuVig:ChaveDashboard
// Duas chaves diferentes: a do Agent só serve para enviar, a do Dashboard só para ler.
var chaveEsperada = builder.Configuration["NexuVig:ChaveApi"];
var chaveDashboard = builder.Configuration["NexuVig:ChaveDashboard"];

if (string.IsNullOrEmpty(chaveEsperada))
{
    app.Logger.LogWarning("NexuVig:ChaveApi não está configurada: todos os envios do Agent vão ser rejeitados (401).");
}

// Só em Development a leitura fica aberta quando a chave do Dashboard não está configurada.
bool leituraAberta = app.Environment.IsDevelopment() && string.IsNullOrEmpty(chaveDashboard);
if (string.IsNullOrEmpty(chaveDashboard))
{
    app.Logger.LogWarning(leituraAberta
        ? "NexuVig:ChaveDashboard não está configurada: a leitura de /api/medicoes está aberta (só em Development)."
        : "NexuVig:ChaveDashboard não está configurada: a leitura de /api/medicoes vai ser rejeitada (401).");
}

static bool ChaveCorresponde(string? esperada, string recebida)
{
    if (string.IsNullOrEmpty(esperada)) return false;
    byte[] a = Encoding.UTF8.GetBytes(esperada);
    byte[] b = Encoding.UTF8.GetBytes(recebida);
    return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
}

app.Use(async (contexto, seguinte) =>
{
    bool eMedicoes = contexto.Request.Path.StartsWithSegments("/api/medicoes");
    bool eEnvio = eMedicoes && HttpMethods.IsPost(contexto.Request.Method);
    bool eLeitura = eMedicoes && HttpMethods.IsGet(contexto.Request.Method);

    if (eEnvio || (eLeitura && !leituraAberta))
    {
        contexto.Request.Headers.TryGetValue("X-Api-Key", out var recebida);

        bool valida = ChaveCorresponde(eEnvio ? chaveEsperada : chaveDashboard, recebida.ToString());

        if (!valida)
        {
            contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await contexto.Response.WriteAsJsonAsync(new { mensagem = "Chave de API inválida." });
            return;
        }
    }

    await seguinte();
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NexuVigContext>();
    db.Database.Migrate();
}

app.MapGet("/", () => Results.Ok(new { projeto = "NexuVig", estado = "Servidor a funcionar com Supabase" }));

app.MapGet("/api/medicoes", async (NexuVigContext db) =>
{
    var computadores = await db.Computadores.Include(c => c.Medicoes).ToListAsync();

    var resultado = computadores.Select(c => {
        var ultima = c.Medicoes.OrderByDescending(m => m.DataHora).FirstOrDefault();
        return new DadosEquipamento
        {
            AgentId = c.AgentId,
            Nome = c.Nome,
            SistemaOperativo = c.SistemaOperativo,
            Processador = c.Processador,
            EnderecosIPv4 = c.EnderecosIPv4,
            UnidadeDisco = c.UnidadeDisco,
            DataHora = new DateTimeOffset(c.UltimaComunicacao, TimeSpan.Zero),
            UtilizacaoCPU = ultima?.UtilizacaoCPU ?? 0,
            UtilizacaoRAM = ultima?.UtilizacaoRAM ?? 0,
            MemoriaTotalGiB = ultima?.MemoriaTotalGiB ?? 0,
            MemoriaDisponivelGiB = ultima?.MemoriaDisponivelGiB ?? 0,
            DiscoTotalGiB = ultima?.DiscoTotalGiB ?? 0,
            DiscoLivreGiB = ultima?.DiscoLivreGiB ?? 0,
            TempoLigadoSegundos = ultima?.TempoLigadoSegundos ?? 0
        };
    }).ToList();

    return Results.Ok(resultado);
});

app.MapPost("/api/medicoes", async (DadosEquipamento dados, NexuVigContext db) =>
{
    var erros = new List<string>();
    if (!Guid.TryParse(dados.AgentId, out var id) || id == Guid.Empty) erros.Add("AgentId inválido.");
    if (string.IsNullOrWhiteSpace(dados.Nome)) erros.Add("Nome obrigatório.");
    if (dados.DataHora == default) erros.Add("DataHora obrigatória.");

    if (erros.Count > 0) return Results.BadRequest(new { mensagem = "Medição rejeitada.", erros });

    var computador = await db.Computadores.FirstOrDefaultAsync(c => c.AgentId == dados.AgentId);

    if (computador == null)
    {
        computador = new Computador
        {
            AgentId = dados.AgentId,
            Nome = dados.Nome,
            SistemaOperativo = dados.SistemaOperativo,
            Processador = dados.Processador
        };
        db.Computadores.Add(computador);
    }

    // Dados do computador que podem mudar (nome, sistema, processador, rede, disco):
    // ficam sempre com o último valor recebido. O computador é identificado pelo AgentId.
    computador.Nome = dados.Nome;
    computador.SistemaOperativo = dados.SistemaOperativo;
    computador.Processador = dados.Processador;
    computador.EnderecosIPv4 = dados.EnderecosIPv4 ?? new();
    computador.UnidadeDisco = dados.UnidadeDisco ?? "";
    // Hora do Server (e não a do Agent): o estado online/offline não pode depender
    // do relógio de cada computador, que pode estar atrasado ou adiantado.
    computador.UltimaComunicacao = DateTime.UtcNow;

    var novaMedicao = new Medicao
    {
        Computador = computador,
        DataHora = dados.DataHora.UtcDateTime,
        UtilizacaoCPU = dados.UtilizacaoCPU,
        UtilizacaoRAM = dados.UtilizacaoRAM,
        MemoriaTotalGiB = dados.MemoriaTotalGiB,
        MemoriaDisponivelGiB = dados.MemoriaDisponivelGiB,
        DiscoTotalGiB = dados.DiscoTotalGiB,
        DiscoLivreGiB = dados.DiscoLivreGiB,
        TempoLigadoSegundos = dados.TempoLigadoSegundos
    };

    db.Medicoes.Add(novaMedicao);
    await db.SaveChangesAsync();

    return Results.Ok(new { mensagem = "Medição guardada com sucesso." });
});

app.Run();
