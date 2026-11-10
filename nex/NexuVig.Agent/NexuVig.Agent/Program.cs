using System.Runtime.Versioning;
using Microsoft.Extensions.Logging.EventLog;
using NexuVig.Agent;

// O Agent usa WMI e o Visualizador de Eventos: só funciona em Windows.
[assembly: SupportedOSPlatform("windows")]

var builder = Host.CreateApplicationBuilder(args);

// Permite correr como Serviço do Windows (arranca com o computador)
builder.Services.AddWindowsService(opcoes =>
{
    opcoes.ServiceName = "NexuVig Agent";
});

// Como serviço não há consola: os registos vão para o Visualizador de Eventos
// (Registos do Windows > Aplicação), com a origem "NexuVig Agent".
builder.Services.Configure<EventLogSettings>(definicoes =>
{
    definicoes.SourceName = "NexuVig Agent";
    definicoes.LogName = "Application";
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();