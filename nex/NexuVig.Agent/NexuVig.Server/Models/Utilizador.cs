namespace NexuVig.Server.Models;

// Conta do Dashboard. A palavra-passe nunca é guardada: só o hash PBKDF2 (ver Seguranca/Passwords.cs).
public class Utilizador
{
    public int Id { get; set; }
    public string NomeUtilizador { get; set; } = "";
    public string Nome { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Perfil { get; set; } = "";
    public DateTime CriadoEm { get; set; }
}
