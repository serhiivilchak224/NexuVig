using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NexuVig.Shared;
using NexuVig.Server.Data;
using NexuVig.Server.Models;
using NexuVig.Server.Seguranca;

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

// Segurança (chaves vindas de User Secrets / variáveis de ambiente):
// - POST /api/registo                 -> aberto, mas limitado; o computador fica "por aprovar"
// - POST /api/medicoes (Agent)        -> X-Agent-Token (token do próprio computador)
//                                        ou X-Api-Key = NexuVig:ChaveApi (chave partilhada)
// - GET  /api/medicoes (Dashboard)    -> X-Api-Key = NexuVig:ChaveDashboard
// - /api/computadores/... (gestão)    -> X-Api-Key = NexuVig:ChaveDashboard
// - /api/utilizadores, /api/autenticacao, /api/avarias -> X-Api-Key = NexuVig:ChaveDashboard
//   (só o Dashboard fala com estes endpoints; é o Dashboard que aplica os perfis)
var chaveEsperada = builder.Configuration["NexuVig:ChaveApi"];
var chaveDashboard = builder.Configuration["NexuVig:ChaveDashboard"];

if (string.IsNullOrEmpty(chaveEsperada))
{
    app.Logger.LogInformation("NexuVig:ChaveApi não está configurada: só são aceites Agents registados (token próprio).");
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

// O Server nunca guarda o token: guarda o hash e compara hashes.
static string HashToken(string token) =>
    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

app.Use(async (contexto, seguinte) =>
{
    bool eMedicoes = contexto.Request.Path.StartsWithSegments("/api/medicoes");
    bool eEnvio = eMedicoes && HttpMethods.IsPost(contexto.Request.Method);
    bool eLeitura = eMedicoes && HttpMethods.IsGet(contexto.Request.Method);
    bool eGestao = contexto.Request.Path.StartsWithSegments("/api/computadores") ||
                   contexto.Request.Path.StartsWithSegments("/api/utilizadores") ||
                   contexto.Request.Path.StartsWithSegments("/api/autenticacao") ||
                   contexto.Request.Path.StartsWithSegments("/api/avarias");

    // Com X-Agent-Token, o token é validado no próprio pedido (precisa do AgentId do corpo).
    bool comToken = eEnvio && contexto.Request.Headers.ContainsKey("X-Agent-Token");

    if ((eEnvio && !comToken) || ((eLeitura || eGestao) && !leituraAberta))
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
            Aprovado = c.Aprovado,
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

// Registo automático: o Agent instalado sem chave pede um token próprio.
// O computador fica "por aprovar" até o administrador o aceitar no Dashboard.
const int MaximoRegistosPorHora = 5;     // por endereço IP
const int MaximoPendentes = 50;          // no total, à espera de aprovação
var registos = new ConcurrentDictionary<string, (int Total, DateTime Inicio)>();

app.MapPost("/api/registo", async (PedidoRegisto pedido, HttpContext contexto, NexuVigContext db) =>
{
    if (!Guid.TryParse(pedido.AgentId, out var id) || id == Guid.Empty ||
        string.IsNullOrWhiteSpace(pedido.Nome) || pedido.Nome.Length > 100)
    {
        return Results.BadRequest(new { mensagem = "Pedido de registo inválido." });
    }

    string ip = contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido";
    var agora = DateTime.UtcNow;
    var contagem = registos.AddOrUpdate(ip,
        _ => (1, agora),
        (_, atual) => agora - atual.Inicio > TimeSpan.FromHours(1) ? (1, agora) : (atual.Total + 1, atual.Inicio));

    if (contagem.Total > MaximoRegistosPorHora)
        return Results.Json(new { mensagem = "Demasiados registos a partir deste endereço. Tenta mais tarde." },
            statusCode: StatusCodes.Status429TooManyRequests);

    var computador = await db.Computadores.FirstOrDefaultAsync(c => c.AgentId == pedido.AgentId);

    // Um computador que já tem token não pode ser registado outra vez (impede roubar a identidade).
    if (computador?.TokenHash is not null)
        return Results.Conflict(new { mensagem = "Este computador já está registado. Para o registar de novo, o administrador tem de o remover no painel." });

    if (computador is null)
    {
        if (await db.Computadores.CountAsync(c => !c.Aprovado) >= MaximoPendentes)
            return Results.Json(new { mensagem = "Há demasiados computadores à espera de aprovação." },
                statusCode: StatusCodes.Status429TooManyRequests);

        computador = new Computador
        {
            AgentId = pedido.AgentId,
            Nome = pedido.Nome.Trim(),
            Aprovado = false,
            UltimaComunicacao = agora
        };
        db.Computadores.Add(computador);
    }

    string token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    computador.TokenHash = HashToken(token);
    await db.SaveChangesAsync();

    app.Logger.LogInformation("Novo registo: {Nome} ({AgentId}), aprovado: {Aprovado}",
        computador.Nome, computador.AgentId, computador.Aprovado);

    return Results.Ok(new RespostaRegisto { Token = token, Aprovado = computador.Aprovado });
});

// Gestão (Dashboard): aprovar ou remover um computador.
app.MapPost("/api/computadores/{agentId}/aprovar", async (string agentId, NexuVigContext db) =>
{
    var computador = await db.Computadores.FirstOrDefaultAsync(c => c.AgentId == agentId);
    if (computador is null) return Results.NotFound(new { mensagem = "Computador não encontrado." });

    computador.Aprovado = true;
    await db.SaveChangesAsync();
    return Results.Ok(new { mensagem = "Computador aprovado." });
});

app.MapDelete("/api/computadores/{agentId}", async (string agentId, NexuVigContext db) =>
{
    var computador = await db.Computadores.FirstOrDefaultAsync(c => c.AgentId == agentId);
    if (computador is null) return Results.NotFound(new { mensagem = "Computador não encontrado." });

    // As medições são apagadas em cascata.
    db.Computadores.Remove(computador);
    await db.SaveChangesAsync();
    return Results.Ok(new { mensagem = "Computador removido." });
});

// Contas do Dashboard (perfis Administrador e Técnico).
static UtilizadorInfo ParaInfo(Utilizador u) => new()
{
    Utilizador = u.NomeUtilizador,
    Nome = u.Nome,
    Perfil = u.Perfil,
    CriadoEm = new DateTimeOffset(u.CriadoEm, TimeSpan.Zero)
};

app.MapPost("/api/autenticacao", async (PedidoAutenticacao pedido, NexuVigContext db) =>
{
    string nome = (pedido.Utilizador ?? "").Trim().ToLowerInvariant();
    var conta = await db.Utilizadores.FirstOrDefaultAsync(u => u.NomeUtilizador == nome);

    // Verifica sempre um hash (mesmo sem conta), para o tempo de resposta ser igual.
    bool valida = Passwords.Verificar(pedido.Password ?? "", conta?.PasswordHash ?? Passwords.HashFicticio);

    return conta is not null && valida
        ? Results.Ok(ParaInfo(conta))
        : Results.Json(new { mensagem = "Utilizador ou palavra-passe incorretos." },
            statusCode: StatusCodes.Status401Unauthorized);
});

app.MapGet("/api/utilizadores", async (NexuVigContext db) =>
    Results.Ok((await db.Utilizadores.OrderBy(u => u.NomeUtilizador).ToListAsync()).Select(ParaInfo)));

app.MapPost("/api/utilizadores", async (NovoUtilizador novo, NexuVigContext db) =>
{
    string nome = (novo.Utilizador ?? "").Trim().ToLowerInvariant();
    var erros = new List<string>();

    if (!System.Text.RegularExpressions.Regex.IsMatch(nome, "^[a-z0-9._-]{3,30}$"))
        erros.Add("O utilizador tem de ter 3 a 30 caracteres: letras sem acentos, números, ponto, hífen ou _.");
    if (string.IsNullOrWhiteSpace(novo.Nome) || novo.Nome.Length > 100)
        erros.Add("O nome é obrigatório (máximo 100 caracteres).");
    if ((novo.Password ?? "").Length < 10)
        erros.Add("A palavra-passe tem de ter pelo menos 10 caracteres.");
    else if (PasswordsFracas.Motivo(novo.Password!, nome, novo.Nome ?? "") is string motivo)
        erros.Add(motivo);
    if (!Perfis.Valido(novo.Perfil))
        erros.Add("Perfil inválido.");

    if (erros.Count > 0) return Results.BadRequest(new { mensagem = string.Join(" ", erros), erros });

    if (await db.Utilizadores.AnyAsync(u => u.NomeUtilizador == nome))
        return Results.Conflict(new { mensagem = "Já existe uma conta com esse utilizador." });

    var conta = new Utilizador
    {
        NomeUtilizador = nome,
        Nome = novo.Nome.Trim(),
        PasswordHash = Passwords.CriarHash(novo.Password!),
        Perfil = novo.Perfil,
        CriadoEm = DateTime.UtcNow
    };
    db.Utilizadores.Add(conta);
    await db.SaveChangesAsync();

    return Results.Ok(ParaInfo(conta));
});

app.MapDelete("/api/utilizadores/{utilizador}", async (string utilizador, NexuVigContext db) =>
{
    string nome = utilizador.Trim().ToLowerInvariant();
    var conta = await db.Utilizadores.FirstOrDefaultAsync(u => u.NomeUtilizador == nome);
    if (conta is null) return Results.NotFound(new { mensagem = "Conta não encontrada." });

    db.Utilizadores.Remove(conta);
    await db.SaveChangesAsync();
    return Results.Ok(new { mensagem = "Conta removida." });
});

// Avarias e intervenções. O autor (nome de quem regista) vem do Dashboard, que sabe quem entrou.
static DateTimeOffset Utc(DateTime data) => new(DateTime.SpecifyKind(data, DateTimeKind.Utc));

static string Limpar(string? texto, int maximo)
{
    string limpo = (texto ?? "").Trim();
    return limpo.Length > maximo ? limpo[..maximo] : limpo;
}

app.MapGet("/api/avarias", async (string? estado, string? agentId, NexuVigContext db) =>
{
    var consulta = db.Avarias.AsQueryable();

    // estado: "abertas" (Aberta + Em curso, por defeito), "resolvidas" ou "todas"
    if (estado is null or "abertas")
        consulta = consulta.Where(a => a.Estado != EstadosAvaria.Resolvida);
    else if (estado == "resolvidas")
        consulta = consulta.Where(a => a.Estado == EstadosAvaria.Resolvida);

    if (!string.IsNullOrWhiteSpace(agentId))
        consulta = consulta.Where(a => a.Computador.AgentId == agentId);

    var linhas = await consulta
        .Select(a => new
        {
            a.Id,
            a.Computador.AgentId,
            Computador = a.Computador.Nome,
            a.Titulo,
            a.Prioridade,
            a.Estado,
            a.Autor,
            a.CriadaEm,
            a.ResolvidaEm,
            NumeroIntervencoes = a.Intervencoes.Count,
            UltimaIntervencao = a.Intervencoes.Max(i => (DateTime?)i.Data)
        })
        .ToListAsync();

    var resultado = linhas
        .Select(a => new AvariaResumo
        {
            Id = a.Id,
            AgentId = a.AgentId,
            Computador = a.Computador,
            Titulo = a.Titulo,
            Prioridade = a.Prioridade,
            Estado = a.Estado,
            Autor = a.Autor,
            CriadaEm = Utc(a.CriadaEm),
            ResolvidaEm = a.ResolvidaEm is DateTime r ? Utc(r) : null,
            NumeroIntervencoes = a.NumeroIntervencoes,
            UltimaAtualizacao = Utc(a.UltimaIntervencao ?? a.CriadaEm)
        })
        // Por resolver primeiro, depois pela prioridade e pela data (mais recentes primeiro).
        .OrderBy(a => a.Estado == EstadosAvaria.Resolvida)
        .ThenBy(a => Prioridades.Ordem(a.Prioridade))
        .ThenByDescending(a => a.UltimaAtualizacao)
        .ToList();

    return Results.Ok(resultado);
});

app.MapGet("/api/avarias/{id:int}", async (int id, NexuVigContext db) =>
{
    var a = await db.Avarias
        .Include(x => x.Computador)
        .Include(x => x.Intervencoes)
        .FirstOrDefaultAsync(x => x.Id == id);

    if (a is null) return Results.NotFound(new { mensagem = "Avaria não encontrada." });

    var intervencoes = a.Intervencoes.OrderBy(i => i.Data).ToList();

    return Results.Ok(new AvariaDetalhe
    {
        Id = a.Id,
        AgentId = a.Computador.AgentId,
        Computador = a.Computador.Nome,
        Titulo = a.Titulo,
        Descricao = a.Descricao,
        Prioridade = a.Prioridade,
        Estado = a.Estado,
        Autor = a.Autor,
        CriadaEm = Utc(a.CriadaEm),
        ResolvidaEm = a.ResolvidaEm is DateTime r ? Utc(r) : null,
        NumeroIntervencoes = intervencoes.Count,
        UltimaAtualizacao = Utc(intervencoes.Count > 0 ? intervencoes[^1].Data : a.CriadaEm),
        Intervencoes = intervencoes.Select(i => new IntervencaoInfo
        {
            Id = i.Id,
            Data = Utc(i.Data),
            Descricao = i.Descricao,
            Autor = i.Autor
        }).ToList()
    });
});

app.MapPost("/api/avarias", async (NovaAvaria nova, NexuVigContext db) =>
{
    var erros = new List<string>();
    if (string.IsNullOrWhiteSpace(nova.Titulo)) erros.Add("O título é obrigatório.");
    if (!Prioridades.Valida(nova.Prioridade)) erros.Add("Prioridade inválida.");
    if (string.IsNullOrWhiteSpace(nova.Autor)) erros.Add("Falta o autor.");

    var computador = await db.Computadores.FirstOrDefaultAsync(c => c.AgentId == nova.AgentId && c.Aprovado);
    if (computador is null) erros.Add("Escolhe um equipamento.");

    if (erros.Count > 0) return Results.BadRequest(new { mensagem = string.Join(" ", erros), erros });

    var avaria = new Avaria
    {
        Computador = computador!,
        Titulo = Limpar(nova.Titulo, 150),
        Descricao = Limpar(nova.Descricao, 4000),
        Prioridade = nova.Prioridade,
        Estado = EstadosAvaria.Aberta,
        Autor = Limpar(nova.Autor, 150),
        CriadaEm = DateTime.UtcNow
    };

    db.Avarias.Add(avaria);
    await db.SaveChangesAsync();

    return Results.Ok(new { id = avaria.Id, mensagem = "Avaria registada." });
});

app.MapPost("/api/avarias/{id:int}/intervencoes", async (int id, NovaIntervencao nova, NexuVigContext db) =>
{
    var avaria = await db.Avarias.FirstOrDefaultAsync(a => a.Id == id);
    if (avaria is null) return Results.NotFound(new { mensagem = "Avaria não encontrada." });

    bool mudaEstado = !string.IsNullOrEmpty(nova.NovoEstado);
    if (mudaEstado && !EstadosAvaria.Valido(nova.NovoEstado))
        return Results.BadRequest(new { mensagem = "Estado inválido." });
    if (string.IsNullOrWhiteSpace(nova.Descricao))
        return Results.BadRequest(new { mensagem = "Descreve o que foi feito." });
    if (string.IsNullOrWhiteSpace(nova.Autor))
        return Results.BadRequest(new { mensagem = "Falta o autor." });

    var agora = DateTime.UtcNow;

    db.Intervencoes.Add(new Intervencao
    {
        AvariaId = avaria.Id,
        Data = agora,
        Descricao = Limpar(nova.Descricao, 4000),
        Autor = Limpar(nova.Autor, 150)
    });

    if (mudaEstado && nova.NovoEstado != avaria.Estado)
    {
        avaria.Estado = nova.NovoEstado!;
        avaria.ResolvidaEm = avaria.Estado == EstadosAvaria.Resolvida ? agora : null;
    }

    await db.SaveChangesAsync();
    return Results.Ok(new { mensagem = "Intervenção registada." });
});

app.MapDelete("/api/avarias/{id:int}", async (int id, NexuVigContext db) =>
{
    var avaria = await db.Avarias.FirstOrDefaultAsync(a => a.Id == id);
    if (avaria is null) return Results.NotFound(new { mensagem = "Avaria não encontrada." });

    // As intervenções são apagadas em cascata.
    db.Avarias.Remove(avaria);
    await db.SaveChangesAsync();
    return Results.Ok(new { mensagem = "Avaria apagada." });
});

app.MapPost("/api/medicoes", async (DadosEquipamento dados, HttpContext contexto, NexuVigContext db) =>
{
    var erros = new List<string>();
    if (!Guid.TryParse(dados.AgentId, out var id) || id == Guid.Empty) erros.Add("AgentId inválido.");
    if (string.IsNullOrWhiteSpace(dados.Nome)) erros.Add("Nome obrigatório.");
    if (dados.DataHora == default) erros.Add("DataHora obrigatória.");

    if (erros.Count > 0) return Results.BadRequest(new { mensagem = "Medição rejeitada.", erros });

    var computador = await db.Computadores.FirstOrDefaultAsync(c => c.AgentId == dados.AgentId);

    if (contexto.Request.Headers.TryGetValue("X-Agent-Token", out var tokenRecebido))
    {
        // Agent registado: o token tem de ser o deste computador.
        if (computador?.TokenHash is null ||
            !ChaveCorresponde(computador.TokenHash, HashToken(tokenRecebido.ToString())))
        {
            return Results.Json(new { mensagem = "Token inválido." },
                statusCode: StatusCodes.Status401Unauthorized);
        }
    }
    else if (computador == null)
    {
        // Chave partilhada (já validada): instalado pelo administrador, fica logo aprovado.
        computador = new Computador
        {
            AgentId = dados.AgentId,
            Nome = dados.Nome,
            SistemaOperativo = dados.SistemaOperativo,
            Processador = dados.Processador,
            Aprovado = true
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

    // Por aprovar: fica só a identificação do computador; as medições ainda não são guardadas.
    if (!computador.Aprovado)
    {
        await db.SaveChangesAsync();
        return Results.Accepted(value: new RespostaMedicao
        {
            Estado = "pendente",
            Mensagem = "Computador à espera de aprovação do administrador."
        });
    }

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

    return Results.Ok(new RespostaMedicao { Estado = "aprovado", Mensagem = "Medição guardada com sucesso." });
});

app.Run();
