
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
- [x] Agent como Serviço do Windows
- [x] Segurança da API (API Key)
- [ ] Login e perfis de utilizador
- [ ] Registo de avarias e intervenções
- [ ] Instalador do Agent

## Como executar

1. Configurar a connection string do Supabase (`ConnectionStrings:SupabaseConnection`) nos User Secrets do projeto NexuVig.Server (nunca no código).
2. Gerar uma chave de API e guardar a **mesma** chave (`NexuVig:ChaveApi`) nos User Secrets do NexuVig.Server e do NexuVig.Agent. O Agent envia-a no cabeçalho `X-Api-Key`; sem chave válida, o Server responde 401.
3. Abrir a solução no Visual Studio e executar o Agent, o Server e o Dashboard em conjunto. Ao arrancar, o Server aplica as migrações do Entity Framework (pasta `Migrations`).

### Agent como Serviço do Windows

1. Publicar o Agent em ficheiro único (não precisa de .NET instalado no computador):
   `dotnet publish nex/NexuVig.Agent/NexuVig.Agent/NexuVig.Agent.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o publish/Agent`
2. Num PowerShell de administrador: copiar `NexuVig.Agent.exe` e `appsettings.json` para `C:\Program Files\NexuVig\Agent` e criar o serviço com `sc.exe create "NexuVig Agent" binPath= "<pasta>\NexuVig.Agent.exe" start= delayed-auto`.
3. Como serviço não há User Secrets: a chave vai para a variável `NexuVig__ChaveApi`, guardada no valor `Environment` (REG_MULTI_SZ) de `HKLM\SYSTEM\CurrentControlSet\Services\NexuVig Agent`, que só os administradores conseguem ler.
4. Os registos do serviço ficam no Visualizador de Eventos (Registos do Windows > Aplicação, origem "NexuVig Agent").
5. O serviço corre como LocalSystem, que só aceita o HTTPS do Server se o certificado for de confiança para o computador (Cert:\LocalMachine\Root). Com o Server online usa-se um certificado verdadeiro.

### Instalador do Agent (Setup.exe)

Instala o Agent como uma aplicação normal do Windows: copia os ficheiros para `C:\Program Files\NexuVig\Agent`, regista o Serviço do Windows `NexuVig Agent`, guarda o endereço do Server e a chave de API no próprio serviço e arranca-o. Aparece em "Aplicações instaladas" e desinstala-se por aí (o serviço é parado e removido).

1. No PC de desenvolvimento: instalar o [Inno Setup 6](https://jrsoftware.org/isdl.php) e executar `installer\criar-instalador.cmd`. Cria `installer\output\NexuVig-Agent-Setup.exe`.
2. No computador a monitorizar (precisa de direitos de administrador): executar o `Setup.exe`, indicar o endereço do Server (HTTPS) e a chave de API.
3. Instalação silenciosa (a chave fica no histórico da linha de comandos; em computadores partilhados, preferir o assistente): `NexuVig-Agent-Setup.exe /VERYSILENT /SERVER=https://servidor:7259 /CHAVE=<chave>`.
4. Se o Server usar um certificado autoassinado, copiar o `.cer` para `installer\servidor.cer` antes de criar o instalador; é instalado como de confiança no computador. Com um certificado verdadeiro não é preciso.
5. O computador tem de conseguir chegar ao endereço do Server. O Server a correr noutro computador (ou em `localhost` de outro PC) não é acessível sem rede ou alojamento online.
6. Verificar que utilizadores normais não leem a chave: `(Get-Acl 'HKLM:\SYSTEM\CurrentControlSet\Services\NexuVig Agent').Access | Format-Table IdentityReference, RegistryRights`.

### Testes com outro computador ou máquina virtual

1. O endereço do Server é configurável (`NexuVig:EnderecosServer`; por defeito `https://localhost:7259`). Para aceitar também a rede Host-Only do VirtualBox:
   `dotnet run --project nex/NexuVig.Agent/NexuVig.Server -- --NexuVig:EnderecosServer='https://localhost:7259;https://192.168.56.1:7259'`
2. Certificado de testes autoassinado (válido para `localhost`, o nome do PC e os IPs), guardado fora do repositório; o caminho e a password ficam nos User Secrets do Server (`Kestrel:Certificates:Default:Path` e `Kestrel:Certificates:Default:Password`). A parte pública (`.cer`) é importada em `Cert:\LocalMachine\Root` no PC do Server e na VM.
3. Firewall: abrir a porta 7259 só para a rede da VM (`192.168.56.0/24`).
4. Na VM: rede "Host-Only", snapshot antes de instalar, e o Agent com `EnderecoServer` = `https://192.168.56.1:7259`.
5. O Dashboard lê o endereço do Server em `NexuVig:EnderecoServer` (appsettings.json).

Para criar novas migrações: `dotnet tool restore` e depois `dotnet ef migrations add NomeDaMigracao --project nex/NexuVig.Agent/NexuVig.Server --output-dir Migrations`.

## Autor

Serhii Vilchak
