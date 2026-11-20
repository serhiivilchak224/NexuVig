using System.Security.Cryptography;

namespace NexuVig.Server.Seguranca;

// Hash de palavras-passe com PBKDF2 (SHA-256, sal aleatório de 16 bytes, 210 000 iterações).
// Formato guardado: pbkdf2-sha256$iteracoes$sal$hash (sal e hash em Base64).
public static class Passwords
{
    private const int Iteracoes = 210_000;
    private const int TamanhoSal = 16;
    private const int TamanhoHash = 32;

    public static string CriarHash(string password)
    {
        byte[] sal = RandomNumberGenerator.GetBytes(TamanhoSal);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, sal, Iteracoes, HashAlgorithmName.SHA256, TamanhoHash);
        return $"pbkdf2-sha256${Iteracoes}${Convert.ToBase64String(sal)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verificar(string password, string guardado)
    {
        string[] partes = guardado.Split('$');
        if (partes.Length != 4 || partes[0] != "pbkdf2-sha256" || !int.TryParse(partes[1], out int iteracoes))
            return false;

        byte[] sal, esperado;
        try
        {
            sal = Convert.FromBase64String(partes[2]);
            esperado = Convert.FromBase64String(partes[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        byte[] calculado = Rfc2898DeriveBytes.Pbkdf2(password, sal, iteracoes, HashAlgorithmName.SHA256, esperado.Length);
        return CryptographicOperations.FixedTimeEquals(calculado, esperado);
    }

    // Usado quando o utilizador não existe, para a resposta demorar o mesmo
    // (assim não se descobre que nomes de utilizador existem pelo tempo de resposta).
    public static readonly string HashFicticio = CriarHash(Guid.NewGuid().ToString());
}
