using NexuVig.Dashboard;
using NexuVig.Dashboard.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<ServicoIdioma>();

// Endereço do Server (configurável em appsettings.json: NexuVig:EnderecoServer).
var enderecoServer = builder.Configuration["NexuVig:EnderecoServer"];
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(string.IsNullOrWhiteSpace(enderecoServer) ? "https://localhost:7259" : enderecoServer)
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
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
