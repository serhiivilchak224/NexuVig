; Instalador do NexuVig Agent (Inno Setup 6.3 ou superior)
;
; Instala o Agent em C:\Program Files\NexuVig\Agent, regista-o como Servico do
; Windows ("NexuVig Agent", arranque automatico atrasado) e guarda o endereco do
; Server e a chave de API no proprio servico (nunca em ficheiros do programa).
;
; Como criar o Setup.exe: executar installer\criar-instalador.cmd
; Instalacao silenciosa: NexuVig-Agent-Setup.exe /VERYSILENT /SERVER=https://... /CHAVE=...

#define NomeApp "NexuVig Agent"
#define VersaoApp "1.0.0"

[Setup]
AppId={{5C0D22E1-4ADD-49C1-B39F-740B6C265FF3}
AppName={#NomeApp}
AppVersion={#VersaoApp}
AppPublisher=Serhii Vilchak
DefaultDirName={autopf}\NexuVig\Agent
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=output
OutputBaseFilename=NexuVig-Agent-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupLogging=yes
UninstallDisplayName={#NomeApp}
UninstallDisplayIcon={app}\NexuVig.Agent.exe

[Languages]
Name: "pt"; MessagesFile: "compiler:Languages\Portuguese.isl"

[Files]
Source: "..\publish\Agent\NexuVig.Agent.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\publish\Agent\appsettings.json"; DestDir: "{app}"; Flags: ignoreversion
; Opcional: se existir installer\servidor.cer, o certificado do Server e instalado
; como de confianca neste computador (necessario com certificados autoassinados).
Source: "servidor.cer"; DestDir: "{tmp}"; Flags: skipifsourcedoesntexist deleteafterinstall

[Code]
const
  NomeServico = 'NexuVig Agent';

var
  PaginaServidor: TInputQueryWizardPage;

function ExecutarSc(const Parametros: String): Integer;
var
  Codigo: Integer;
begin
  if not Exec(ExpandConstant('{sys}\sc.exe'), Parametros, '', SW_HIDE, ewWaitUntilTerminated, Codigo) then
    Codigo := -1;
  Result := Codigo;
end;

procedure RemoverServicoExistente;
begin
  // "sc query" devolve 0 quando o servico existe (reinstalacao ou atualizacao).
  if ExecutarSc('query "' + NomeServico + '"') = 0 then
  begin
    ExecutarSc('stop "' + NomeServico + '"');
    Sleep(3000);
    ExecutarSc('delete "' + NomeServico + '"');
    Sleep(1000);
  end;
end;

function ValidarLigacao(var Mensagem: String): Boolean;
var
  Endereco, Chave: String;
begin
  Result := False;
  Endereco := Trim(PaginaServidor.Values[0]);
  Chave := Trim(PaginaServidor.Values[1]);

  if Pos('https://', LowerCase(Endereco)) <> 1 then
    Mensagem := 'O endereço do Server tem de começar por https://'
  else if Length(Chave) < 16 then
    Mensagem := 'A chave de API parece demasiado curta (mínimo de 16 caracteres).'
  else
    Result := True;
end;

procedure InitializeWizard;
begin
  PaginaServidor := CreateInputQueryPage(wpSelectDir,
    'Ligação ao Server',
    'Onde está o Server do NexuVig?',
    'Indica o endereço do Server (HTTPS) e a chave de API fornecida pelo administrador.');
  PaginaServidor.Add('Endereço do Server:', False);
  PaginaServidor.Add('Chave de API:', True);

  // Valores por defeito; na instalação silenciosa vêm de /SERVER= e /CHAVE=
  PaginaServidor.Values[0] := ExpandConstant('{param:SERVER|https://localhost:7259}');
  PaginaServidor.Values[1] := ExpandConstant('{param:CHAVE|}');
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Mensagem: String;
begin
  Result := True;
  if CurPageID = PaginaServidor.ID then
  begin
    if not ValidarLigacao(Mensagem) then
    begin
      MsgBox(Mensagem, mbError, MB_OK);
      Result := False;
    end;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Mensagem: String;
begin
  Result := '';
  // Valida tambem na instalação silenciosa, onde as páginas não aparecem.
  if not ValidarLigacao(Mensagem) then
  begin
    Result := Mensagem;
    Exit;
  end;
  RemoverServicoExistente;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Executavel, Certificado, Ambiente: String;
  Codigo: Integer;
begin
  if CurStep <> ssPostInstall then
    Exit;

  Executavel := ExpandConstant('{app}\NexuVig.Agent.exe');

  Codigo := ExecutarSc('create "' + NomeServico + '" binPath= "' + Executavel +
    '" start= delayed-auto DisplayName= "' + NomeServico + '"');
  if Codigo <> 0 then
  begin
    MsgBox('Não foi possível criar o serviço do Windows (código ' + IntToStr(Codigo) +
      '). O registo da instalação está em %TEMP%.', mbError, MB_OK);
    Exit;
  end;

  ExecutarSc('description "' + NomeServico +
    '" "Envia ao Server do NexuVig os dados de CPU, RAM e disco deste computador."');
  // Se o serviço falhar, o Windows tenta reiniciá-lo (3 vezes, com 1 minuto de intervalo).
  ExecutarSc('failure "' + NomeServico +
    '" reset= 86400 actions= restart/60000/restart/60000/restart/60000');

  // O serviço não lê User Secrets: o endereço e a chave ficam em variáveis de ambiente
  // do próprio serviço (valor Environment, REG_MULTI_SZ), lidas pelo Agent.
  Ambiente := 'NexuVig__EnderecoServer=' + Trim(PaginaServidor.Values[0]) + #0 +
              'NexuVig__ChaveApi=' + Trim(PaginaServidor.Values[1]);
  RegWriteMultiStringValue(HKLM, 'SYSTEM\CurrentControlSet\Services\' + NomeServico,
    'Environment', Ambiente);

  Certificado := ExpandConstant('{tmp}\servidor.cer');
  if FileExists(Certificado) then
    Exec(ExpandConstant('{sys}\certutil.exe'), '-addstore -f Root "' + Certificado + '"',
      '', SW_HIDE, ewWaitUntilTerminated, Codigo);

  ExecutarSc('start "' + NomeServico + '"');
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  // Pára e remove o serviço antes de os ficheiros serem apagados.
  // O identificador do Agent (C:\ProgramData\NexuVig\agent-id.txt) mantém-se,
  // para o computador não aparecer duplicado se o Agent for reinstalado.
  if CurUninstallStep = usUninstall then
    RemoverServicoExistente;
end;
