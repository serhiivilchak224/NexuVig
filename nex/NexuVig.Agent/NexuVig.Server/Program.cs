using System.Security.Cryptography;
using System.Text;
using NexuVig.Shared;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    EnvironmentName = Environments.Development
});

builder.WebHost.UseUrls("https://localhost:7259");

// Permitir que o Dashboard aceda ao Server (CORS)
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();
app.UseCors();

// Segurança: o envio de medições exige a chave de API (cabeçalho X-Api-Key).
// A chave vem dos User Secrets / variáveis de ambiente, nunca do código.
var chaveEsperada = builder.Configuration["NexuVig:ChaveApi"];

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

// Lista em memória para guardar os dados recebidos
var ultimasMedicoes = new List<DadosEquipamento>();

app.MapGet("/", () => Results.Ok(new { projeto = "NexuVig", estado = "Servidor a funcionar" }));

// Rota para o Dashboard LER os dados (Isto resolve o erro 405)
app.MapGet("/api/medicoes", () => Results.Ok(ultimasMedicoes));

// Rota para o Agent ENVIAR os dados
app.MapPost("/api/medicoes", (DadosEquipamento dados) =>
{
    var erros = new List<string>();
    if (!Guid.TryParse(dados.AgentId, out var id) || id == Guid.Empty) erros.Add("AgentId inválido.");
    if (string.IsNullOrWhiteSpace(dados.Nome)) erros.Add("Nome obrigatório.");
    if (dados.DataHora == default) erros.Add("DataHora obrigatória.");

    if (erros.Count > 0) return Results.BadRequest(new { mensagem = "Medição rejeitada.", erros });

    // Atualiza a lista: se o PC já lá estiver, substitui os dados antigos; senão, adiciona.
    var index = ultimasMedicoes.FindIndex(m => m.AgentId == dados.AgentId);
    if (index >= 0) ultimasMedicoes[index] = dados;
    else ultimasMedicoes.Add(dados);

    app.Logger.LogInformation("Medição recebida de {Nome} (Total de PCs: {Total})", dados.Nome, ultimasMedicoes.Count);

    return Results.Ok(new { mensagem = "Medição recebida com sucesso." });
});

app.Run();