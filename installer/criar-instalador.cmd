@echo off
rem Cria o instalador do NexuVig Agent: installer\output\NexuVig-Agent-Setup.exe
rem Requisitos: SDK do .NET 10 e Inno Setup 6.3 ou superior (jrsoftware.org).
setlocal
cd /d "%~dp0\.."

echo [1/2] A publicar o Agent (ficheiro unico, sem precisar de .NET no destino)...
dotnet publish nex\NexuVig.Agent\NexuVig.Agent\NexuVig.Agent.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o publish\Agent
if errorlevel 1 exit /b 1

set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%ProgramFiles%\Inno Setup 7\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%ProgramFiles(x86)%\Inno Setup 7\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%LOCALAPPDATA%\Programs\Inno Setup 7\ISCC.exe"
if not exist "%ISCC%" goto semInno

echo [2/2] A criar o instalador...
"%ISCC%" installer\NexuVig-Agent.iss
if errorlevel 1 exit /b 1

echo.
echo Instalador criado em installer\output\NexuVig-Agent-Setup.exe
exit /b 0

:semInno
echo Inno Setup 6 ou 7 nao encontrado. Instala-o em https://jrsoftware.org/isdl.php e volta a executar.
exit /b 1
