using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NexuVig.Shared;
using NexuVig.Server.Data;
using NexuVig.Server.Models;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    EnvironmentName = Environments.Development
});

// Endereço(s) onde o Server ouve. Por defeito, só no próprio computador.
// Para testes com outros computadores (ex.: VM), arrancar com
// --NexuVig:EnderecosServer="https://localhost:7259;https://192.168.56.1:7259"
var enderecos = builder.Configuration["NexuVig:EnderecosServer"];
builder.WebHost.UseUrls(string.IsNullOrWhiteSpace(enderecos) ? "https://localhost:7259" : enderecos);

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var connectionString = builder.Configuration.GetConnectionString("SupabaseConnection");
builder.Services.AddDbContext<NexuVigContext>(options =>
    options.UseNpgsql(connectionString));

var app = builder.Build();
app.UseCors();

// Segurança: o envio de medições exige a chave de API (cabeçalho X-Api-Key).
// A chave vem dos User Secrets / variáveis de ambiente, nunca do código.
var chaveEsperada = builder.Configuration["NexuVig:ChaveApi"];

if (string.IsNullOrEmpty(chaveEsperada))
{
    app.Logger.LogWarning("NexuVig:ChaveApi não está configurada: todos os envios do Agent vão ser rejeitados (401).");
}

app.Use(async (contexto, seguinte) =>
{
    bool eEnvio = HttpMethods.IsPost(contexto.Request.Method) &&
                  contexto.Request.Path.StartsWithSegments("/api/medicoes");

    if (eEnvio)
    {
        contexto.Request.Headers.TryGetValue("X-Api-Key", out var recebida);

        byte[] esperada = Encoding.UTF8.GetBytes(chaveEsperada ?? "");
        byte[] enviada = Encoding.UTF8.GetBytes(recebida.ToString());

        bool valida = !string.IsNullOrEmpty(chaveEsperada) &&
                      esperada.Length == enviada.Length &&
                      CryptographicOperations.FixedTimeEquals(esperada, enviada);

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
