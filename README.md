
# NexuVig

Sistema de monitorização e gestão de equipamentos informáticos, desenvolvido como Prova de Aptidão Profissional (PAP) do curso Técnico de Gestão de Equipamentos Informáticos (ETPR).

O NexuVig recolhe informação dos computadores (CPU, RAM, disco, estado da comunicação), mostra-a num painel central e assinala alertas, para passar de uma manutenção reativa para uma manutenção preventiva.

## Componentes

| Projeto | Função |
|---|---|
| NexuVig.Agent | Worker Service que corre em cada computador e envia medições a cada 30 segundos |
| NexuVig.Server | API (ASP.NET Core) que valida as medições e as guarda no Supabase (PostgreSQL) |
| NexuVig.Shared | Modelos partilhados entre o Agent e o Server |
| NexuVig.Dashboard | Painel web em Blazor, com tema claro/escuro, alertas e página de detalhes |

## Tecnologias

C# (.NET 10), ASP.NET Core, Blazor, Entity Framework Core, PostgreSQL (Supabase).

## Estado do projeto

- [x] Agent a enviar medições ao Server
- [x] Base de dados na nuvem (Supabase)
- [x] Dashboard com alertas e página de detalhes
- [ ] Agent como Serviço do Windows
- [x] Segurança da API (API Key)
- [ ] Login e perfis de utilizador
- [ ] Registo de avarias e intervenções
- [ ] Instalador do Agent

## Como executar

1. Configurar a connection string do Supabase (`ConnectionStrings:SupabaseConnection`) nos User Secrets do projeto NexuVig.Server (nunca no código).
2. Gerar uma chave de API e guardar a **mesma** chave (`NexuVig:ChaveApi`) nos User Secrets do NexuVig.Server e do NexuVig.Agent. O Agent envia-a no cabeçalho `X-Api-Key`; sem chave válida, o Server responde 401.
3. Abrir a solução no Visual Studio e executar o Agent, o Server e o Dashboard em conjunto. Ao arrancar, o Server aplica as migrações do Entity Framework (pasta `Migrations`).

Para criar novas migrações: `dotnet tool restore` e depois `dotnet ef migrations add NomeDaMigracao --project nex/NexuVig.Agent/NexuVig.Server --output-dir Migrations`.

## Autor

Serhii Vilchak
