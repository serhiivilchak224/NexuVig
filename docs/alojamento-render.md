# Alojar o NexuVig online (Render + Supabase)

Objetivo: o Server e o Dashboard ficam sempre online, para o Agent do PC da escola e o Dashboard de casa não dependerem de nenhum PC ligado. A base de dados continua no Supabase.

## Antes de começar

- Conta em [render.com](https://render.com) (entrar com o GitHub).
- A branch `alojamento-online` (ou o `master`, depois de fundida) no GitHub.
- A connection string do Supabase (Session pooler).
- Plano gratuito do Render: os serviços **adormecem após 15 minutos sem pedidos** e demoram cerca de 1 minuto a acordar. Enquanto o Server dorme, o Agent falha o envio e tenta de novo (as medições desse intervalo perdem-se). Para a demonstração em julho, abrir o Dashboard um minuto antes.

## 1. Gerar as chaves

Em PowerShell (funciona no Windows PowerShell 5.1 e no PowerShell 7; uma de cada vez; guardar num sítio seguro, por exemplo um gestor de palavras-passe):

```powershell
$b = New-Object byte[] 32
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b)
[Convert]::ToBase64String($b)
```

Precisas de **duas chaves diferentes**:

| Chave | Quem a usa |
|---|---|
| `ChaveApi` | Agent (envia medições) e Server |
| `ChaveDashboard` | Dashboard (lê dados) e Server |

Escolhe também a palavra-passe do administrador do Dashboard (longa, não reutilizada).

## 2. Criar os serviços (Blueprint)

1. No Render: **New > Blueprint** e escolher o repositório `nexuvig`, branch `alojamento-online`.
2. O Render lê o `render.yaml` e propõe `nexuvig-server` e `nexuvig-dashboard`. Se algum campo for rejeitado, confirmar o nome em [Blueprint YAML Reference](https://render.com/docs/blueprint-spec).
3. Preencher as variáveis que o Render pede:

| Serviço | Variável | Valor |
|---|---|---|
| nexuvig-server | `ConnectionStrings__SupabaseConnection` | connection string do Supabase |
| nexuvig-server | `NexuVig__ChaveApi` | chave do Agent |
| nexuvig-server | `NexuVig__ChaveDashboard` | chave do Dashboard |
| nexuvig-dashboard | `NexuVig__EnderecoServer` | `https://nexuvig-server.onrender.com` (o endereço real aparece no painel do Server) |
| nexuvig-dashboard | `NexuVig__ChaveDashboard` | a **mesma** chave do Dashboard |
| nexuvig-dashboard | `NexuVig__Admin__Utilizador` | nome do administrador |
| nexuvig-dashboard | `NexuVig__Admin__Password` | palavra-passe do administrador |

4. Esperar o primeiro deploy (alguns minutos). Ao arrancar, o Server aplica as migrações na base de dados.

## 3. Testar

```powershell
# Deve responder 200 com {"projeto":"NexuVig",...}
curl https://nexuvig-server.onrender.com/

# Sem chave: 401. Com a chave do Dashboard: 200 e a lista de computadores.
curl https://nexuvig-server.onrender.com/api/medicoes
curl -H "X-Api-Key: <ChaveDashboard>" https://nexuvig-server.onrender.com/api/medicoes
```

Abrir `https://nexuvig-dashboard.onrender.com`: deve aparecer o ecrã de entrada; depois do login, o painel. O botão "Sair" termina a sessão. Após 5 tentativas falhadas, o IP fica bloqueado 10 minutos.

## 4. Ligar o Agent

Instalar o Agent no PC da escola com o endereço e a chave do Agent (o certificado do Render é verdadeiro, não é preciso `servidor.cer`):

```
NexuVig-Agent-Setup.exe /VERYSILENT /SERVER=https://nexuvig-server.onrender.com /CHAVE=<ChaveApi>
```

ou pelo assistente, indicando os mesmos valores. Em computadores já instalados, reinstalar com o novo endereço e chave.

## 5. Manutenção

- **Rodar uma chave**: gerar nova, atualizar a variável no Render (o serviço reinicia) e, no caso da `ChaveApi`, reinstalar os Agents.
- **Desenvolvimento local**: continua como antes (User Secrets). Sem `ChaveDashboard` e sem `Admin:Password`, o Server e o Dashboard ficam abertos **apenas em Development**. Para testar o login localmente, definir `NexuVig:Admin:Password` nos User Secrets do Dashboard e `NexuVig:ChaveDashboard` nos dois projetos.
- **Segurança**: as chaves e a palavra-passe só existem nas variáveis do Render e nos User Secrets, nunca no Git.
