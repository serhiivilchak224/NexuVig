using NexuVig.Agent;

var builder = Host.CreateApplicationBuilder(args);

// Permite correr como Serviço do Windows (arranca com o computador)
builder.Services.AddWindowsService(opcoes =>
{
    opcoes.ServiceName = "NexuVig Agent";
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();