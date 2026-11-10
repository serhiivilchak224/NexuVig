using System.Collections.Concurrent;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using NexuVig.Dashboard;
using NexuVig.Dashboard.Components;

var builder = WebApplication.CreateBuilder(args);

// Alojamento online (Render): a variável PORT define a porta (HTTP atrás do proxy da plataforma).
var porta = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(porta))
    builder.WebHost.UseUrls($"http://0.0.0.0:{porta}");

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<ServicoIdioma>();

// Endereço do Server (NexuVig:EnderecoServer) e chave de leitura (NexuVig:ChaveDashboard).
// A chave vai no cabeçalho X-Api-Key e só existe no servidor do Dashboard: o browser nunca a vê.
var enderecoServer = builder.Configuration["NexuVig:EnderecoServer"];
var chaveDashboard = builder.Configuration["NexuVig:ChaveDashboard"];
builder.Services.AddScoped(sp =>
{
    var cliente = new HttpClient
    {
        BaseAddress = new Uri(string.IsNullOrWhiteSpace(enderecoServer) ? "https://localhost:7259" : enderecoServer)
    };
    if (!string.IsNullOrEmpty(chaveDashboard))
        cliente.DefaultRequestHeaders.Add("X-Api-Key", chaveDashboard);
    return cliente;
});

// Login de administrador (cookie). Utilizador e password vêm de NexuVig:Admin:Utilizador / NexuVig:Admin:Password
// (User Secrets ou variáveis de ambiente), nunca do código.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opcoes =>
    {
        opcoes.Cookie.Name = "nexuvig.auth";
        opcoes.Cookie.HttpOnly = true;
        opcoes.Cookie.SameSite = SameSiteMode.Lax;
        opcoes.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        opcoes.ExpireTimeSpan = TimeSpan.FromHours(8);
        opcoes.SlidingExpiration = true;
        opcoes.LoginPath = "/entrar";
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

// Atrás do proxy do Render o HTTPS é tratado pela plataforma (e o proxy envia X-Forwarded-Proto,
// ativado com ASPNETCORE_FORWARDEDHEADERS_ENABLED=true no Dockerfile).
if (string.IsNullOrWhiteSpace(porta))
    app.UseHttpsRedirection();

app.UseAuthentication();

var nomeAdmin = builder.Configuration["NexuVig:Admin:Utilizador"];
if (string.IsNullOrWhiteSpace(nomeAdmin)) nomeAdmin = "admin";
var passwordAdmin = builder.Configuration["NexuVig:Admin:Password"];

// Em Development, sem password configurada, o login fica desligado (para trabalhar localmente).
// Em qualquer outro ambiente, sem password nada é acessível.
bool loginAtivo = !(app.Environment.IsDevelopment() && string.IsNullOrEmpty(passwordAdmin));
if (!loginAtivo)
    app.Logger.LogWarning("NexuVig:Admin:Password não está configurada: login desligado (só em Development).");
else if (string.IsNullOrEmpty(passwordAdmin))
    app.Logger.LogWarning("NexuVig:Admin:Password não está configurada: ninguém consegue entrar.");

static byte[] Hash(string texto) => SHA256.HashData(Encoding.UTF8.GetBytes(texto));

string[] extensoesPublicas = [".css", ".js", ".map", ".png", ".ico", ".svg", ".woff", ".woff2", ".ttf"];

// Porta de entrada: só utilizadores autenticados chegam às páginas e ao /_blazor.
app.Use(async (contexto, seguinte) =>
{
    if (!loginAtivo || contexto.User.Identity?.IsAuthenticated == true)
    {
        await seguinte();
        return;
    }

    var caminho = contexto.Request.Path;
    bool publico = caminho.Equals("/entrar", StringComparison.OrdinalIgnoreCase) ||
                   caminho.Equals("/sair", StringComparison.OrdinalIgnoreCase) ||
                   caminho.Equals("/saude", StringComparison.OrdinalIgnoreCase) ||
                   extensoesPublicas.Contains(Path.GetExtension(caminho.Value ?? ""), StringComparer.OrdinalIgnoreCase);

    if (publico)
    {
        await seguinte();
    }
    else if (caminho.StartsWithSegments("/_blazor"))
    {
        contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
    }
    else
    {
        contexto.Response.Redirect("/entrar");
    }
});

app.UseAntiforgery();

// Limite de tentativas: 5 falhas em 10 minutos por endereço IP.
var falhas = new ConcurrentDictionary<string, (int Total, DateTime Inicio)>();

static string PaginaEntrar(string? erro)
{
    string aviso = erro is null ? "" : "<p class=\"erro\">" + erro + "</p>";
    return $$"""
<!DOCTYPE html>
<html lang="pt">
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width, initial-scale=1" />
<title>NexuVig – Entrar</title>
<style>
  body { font-family: system-ui, sans-serif; background: #0f172a; color: #e2e8f0; display: grid; place-items: center; min-height: 100vh; margin: 0; }
  form { background: #1e293b; padding: 2rem; border-radius: 12px; width: min(22rem, 90vw); display: grid; gap: .8rem; }
  h1 { margin: 0 0 .5rem; font-size: 1.4rem; }
  label { display: grid; gap: .25rem; font-size: .9rem; }
  input { padding: .6rem; border-radius: 8px; border: 1px solid #475569; background: #0f172a; color: inherit; font-size: 1rem; }
  button { padding: .7rem; border: 0; border-radius: 8px; background: #3b82f6; color: white; font-size: 1rem; cursor: pointer; }
  .erro { color: #fca5a5; font-size: .9rem; margin: 0; }
</style>
</head>
<body>
<form method="post" action="/entrar">
  <h1>NexuVig</h1>
  {{aviso}}
  <label>Utilizador <input name="utilizador" autocomplete="username" required autofocus /></label>
  <label>Palavra-passe <input name="password" type="password" autocomplete="current-password" required /></label>
  <button type="submit">Entrar</button>
</form>
</body>
</html>
""";
}

app.MapGet("/saude", () => Results.Text("ok"));

app.MapGet("/entrar", () => Results.Content(PaginaEntrar(null), "text/html; charset=utf-8"));

app.MapPost("/entrar", async (HttpContext contexto) =>
{
    string ip = contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido";

    if (falhas.TryGetValue(ip, out var estado))
    {
        if (DateTime.UtcNow - estado.Inicio > TimeSpan.FromMinutes(10))
            falhas.TryRemove(ip, out _);
        else if (estado.Total >= 5)
            return Results.Content(PaginaEntrar("Demasiadas tentativas. Tenta novamente dentro de alguns minutos."),
                "text/html; charset=utf-8", Encoding.UTF8, StatusCodes.Status429TooManyRequests);
    }

    var formulario = await contexto.Request.ReadFormAsync();
    string utilizador = formulario["utilizador"].ToString();
    string password = formulario["password"].ToString();

    // Os dois campos são sempre comparados (sem sair cedo), em tempo constante.
    bool utilizadorOk = CryptographicOperations.FixedTimeEquals(Hash(utilizador), Hash(nomeAdmin!));
    bool passwordOk = !string.IsNullOrEmpty(passwordAdmin) &&
                      CryptographicOperations.FixedTimeEquals(Hash(password), Hash(passwordAdmin));

    if (!(utilizadorOk && passwordOk))
    {
        falhas.AddOrUpdate(ip,
            _ => (1, DateTime.UtcNow),
            (_, atual) => (atual.Total + 1, atual.Inicio));
        return Results.Content(PaginaEntrar("Utilizador ou palavra-passe incorretos."),
            "text/html; charset=utf-8", Encoding.UTF8, StatusCodes.Status401Unauthorized);
    }

    falhas.TryRemove(ip, out _);

    var identidade = new ClaimsIdentity(
        [new Claim(ClaimTypes.Name, nomeAdmin!), new Claim(ClaimTypes.Role, "Administrador")],
        CookieAuthenticationDefaults.AuthenticationScheme);

    await contexto.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(identidade));

    return Results.Redirect("/");
}).DisableAntiforgery();

app.MapPost("/sair", async (HttpContext contexto) =>
{
    await contexto.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/entrar");
}).DisableAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
