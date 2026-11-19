
# NexuVig

Sistema de monitorização e gestão de equipamentos informáticos, desenvolvido como Prova de Aptidão Profissional (PAP) do curso Técnico de Gestão de Equipamentos Informáticos (ETPR).

O NexuVig recolhe informação dos computadores (CPU, RAM, disco, estado da comunicação), mostra-a num painel central e assinala alertas, para passar de uma manutenção reativa para uma manutenção preventiva.

## Componentes

| Projeto | Função |
|---|---|
| NexuVig.Agent | Worker Service que corre em cada computador e envia medições a cada 30 segundos |
| NexuVig.Server | API (ASP.NET Core) que valida as medições e as guarda no Supabase (PostgreSQL) |
| NexuVig.App | Programa com janela (WinForms) onde o utilizador vê os dados do seu computador e o estado da ligação |
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
- [x] Login no Dashboard com contas e perfis (Administrador e Técnico)
- [x] Registo de avarias e intervenções
- [x] Instalador (Setup.exe) com o Agent e o programa NexuVig
- [x] Registo automático dos computadores, com aprovação do administrador
- [x] Página de descarga e instalador criado automaticamente (GitHub Actions)

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

### Registo automático e aprovação

Quem instala o NexuVig a partir da página de descarga não precisa de chave:

1. O Agent pede um registo ao Server (`POST /api/registo`) e recebe um **token próprio**, guardado em `C:\ProgramData\NexuVig\seguro` (pasta que só o sistema e os administradores leem). O Server guarda apenas o hash (SHA-256) do token.
2. O computador aparece no Dashboard em **Por aprovar**. Enquanto não for aprovado, o Server não guarda as suas medições (responde 202).
3. O administrador **aprova** ou **rejeita** com um clique. Depois de aprovado, as medições passam a contar.

Proteções: limite de 5 registos por hora por endereço IP, máximo de 50 computadores por aprovar, e um computador que já tem token não pode ser registado outra vez (o administrador tem de o remover primeiro). A chave partilhada (`NexuVig:ChaveApi`) continua a funcionar para instalações feitas pelo administrador, que ficam logo aprovadas.

### Contas e perfis

O Dashboard tem dois perfis:

| Perfil | Pode |
|---|---|
| Administrador | ver tudo, aprovar, rejeitar e remover computadores, gerir utilizadores |
| Técnico | ver o painel e os detalhes dos computadores (por exemplo, um professor) |

- As contas criam-se na página **Utilizadores** (só administradores) e ficam na tabela `Utilizadores` da base de dados. A palavra-passe nunca é guardada: o Server guarda um hash PBKDF2 (SHA-256, sal aleatório, 210 000 iterações).
- A conta definida em `NexuVig:Admin:Utilizador` / `NexuVig:Admin:Password` continua a funcionar como **conta de recuperação** (sempre Administrador), para não ficar sem acesso se as outras forem apagadas.
- Os endpoints de contas (`/api/autenticacao`, `/api/utilizadores`) só aceitam pedidos com a chave do Dashboard: os utilizadores nunca falam diretamente com o Server. Os perfis são aplicados pelo Dashboard, que esconde e bloqueia as ações que o perfil não permite.
- Ao criar uma conta, a palavra-passe tem de ter pelo menos 8 caracteres e não pode ser uma das palavras-passe mais comuns (listas de palavras-passe divulgadas). O botão **Gerar** cria uma palavra-passe aleatória fácil de ler (ex.: `k7Pm-x3Qa-9vRt`).
- Limitação conhecida: uma sessão aberta dura até 8 horas, mesmo que a conta seja removida entretanto.

### Avarias e intervenções

- Página **Avarias**: lista das avarias (por resolver, resolvidas ou todas), ordenadas por prioridade, e formulário para registar uma avaria num equipamento (título, descrição, prioridade Baixa/Média/Alta).
- Cada avaria tem um **histórico de intervenções** (o que foi feito, quem fez e quando). Ao registar uma intervenção pode mudar-se o estado: Aberta → Em curso → Resolvida (ou reabrir). O tempo até à resolução fica registado.
- O autor é sempre a conta que entrou no Dashboard (nome e utilizador), não um campo escrito à mão.
- Administradores e técnicos registam avarias e intervenções; só administradores apagam avarias.
- O painel mostra uma etiqueta com o número de avarias por resolver de cada equipamento, e a página do equipamento lista-as, com um botão para registar uma nova.
- Tabelas `Avarias` e `Intervencoes` (migração `AdicionarAvarias`). Remover um equipamento apaga também as suas avarias.

### Página de descarga e instalador automático

- `site/index.html` é a página de descarga (site estático no Render, definido no `render.yaml`).
- `.github/workflows/compilar.yml` compila a solução em cada envio para o GitHub e cria o `NexuVig-Setup.exe`. Nas branches `master` e `alojamento-online` publica-o em Releases; o botão da página aponta sempre para a versão mais recente.

### Instalador (Setup.exe)

Instala o NexuVig como uma aplicação normal do Windows: copia o Agent e o programa NexuVig para `C:\Program Files\NexuVig\Agent`, regista o Serviço do Windows `NexuVig Agent`, guarda o endereço do Server (e a chave de API, se for indicada) no próprio serviço e arranca-o. Aparece em "Aplicações instaladas" e desinstala-se por aí (o serviço é parado e removido).

1. No PC de desenvolvimento: instalar o [Inno Setup 6](https://jrsoftware.org/isdl.php) e executar `installer\criar-instalador.cmd`. Cria `installer\output\NexuVig-Setup.exe`.
2. No computador a monitorizar (precisa de direitos de administrador): executar o `Setup.exe`, o endereço do Server já vem preenchido e a chave de API é opcional (sem chave, o computador fica por aprovar).
3. Instalação silenciosa (a chave fica no histórico da linha de comandos; em computadores partilhados, preferir o assistente): `NexuVig-Setup.exe /VERYSILENT /SERVER=https://servidor:7259 /CHAVE=<chave>`.
4. Se o Server usar um certificado autoassinado, copiar o `.cer` para `installer\servidor.cer` antes de criar o instalador; é instalado como de confiança no computador. Com um certificado verdadeiro não é preciso.
5. O computador tem de conseguir chegar ao endereço do Server. O Server a correr noutro computador (ou em `localhost` de outro PC) não é acessível sem rede ou alojamento online.
6. Verificar que utilizadores normais não leem a chave: `(Get-Acl 'HKLM:\SYSTEM\CurrentControlSet\Services\NexuVig Agent').Access | Format-Table IdentityReference, RegistryRights`.

### Alojamento online (Render)

O Server e o Dashboard podem correr online (Docker, Render), com a base de dados no Supabase. O Dashboard tem login de administrador e o Server exige duas chaves: `NexuVig:ChaveApi` (Agent, envio) e `NexuVig:ChaveDashboard` (Dashboard, leitura). Ficheiros: `Dockerfile.server`, `Dockerfile.dashboard`, `render.yaml`. Passo a passo em [docs/alojamento-render.md](docs/alojamento-render.md).

### Testes com outro computador ou máquina virtual

1. O endereço do Server é configurável (`NexuVig:EnderecosServer`; por defeito `https://localhost:7259`). Para aceitar também a rede Host-Only do VirtualBox:
   `dotnet run --project nex/NexuVig.Agent/NexuVig.Server -- --NexuVig:EnderecosServer='https://localhost:7259;https://192.168.56.1:7259'`
2. Certificado de testes autoassinado (válido para `localhost`, o nome do PC e os IPs), guardado fora do repositório; o caminho e a password ficam nos User Secrets do Server (`Kestrel:Certificates:Default:Path` e `Kestrel:Certificates:Default:Password`). A parte pública (`.cer`) é importada em `Cert:\LocalMachine\Root` no PC do Server e na VM.
3. Firewall: abrir a porta 7259 só para a rede da VM (`192.168.56.0/24`).
4. Na VM: rede "Host-Only", snapshot antes de instalar, e o Agent com `EnderecoServer` = `https://192.168.56.1:7259`.
5. O Dashboard lê o endereço do Server em `NexuVig:EnderecoServer` (appsettings.json).

**Segurança da base de dados (Supabase):** todas as tabelas têm Row Level Security (RLS) ativo e sem políticas (migração `AtivarRls`). Assim, a API automática do Supabase (Data API) não consegue ler nem escrever nada; só o Server, que se liga como dono das tabelas, tem acesso. Ao criar uma tabela nova, ativar o RLS na mesma migração.

Para criar novas migrações: `dotnet tool restore` e depois `dotnet ef migrations add NomeDaMigracao --project nex/NexuVig.Agent/NexuVig.Server --output-dir Migrations`.

## Autor

Serhii Vilchak
