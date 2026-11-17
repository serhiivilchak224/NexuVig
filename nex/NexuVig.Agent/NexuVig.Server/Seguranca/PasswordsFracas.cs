namespace NexuVig.Server.Seguranca;

// Recusa palavras-passe fáceis de adivinhar ao criar contas.
// Estas são as primeiras que um atacante experimenta (listas de palavras-passe divulgadas).
public static class PasswordsFracas
{
    // Palavras-passe comuns (com 8 ou mais caracteres, o mínimo exigido).
    private static readonly HashSet<string> Comuns = new(StringComparer.OrdinalIgnoreCase)
    {
        "12345678", "123456789", "87654321", "11111111", "00000000", "12341234", "11223344",
        "password", "password1", "passw0rd", "qwertyui", "qwerty123", "1q2w3e4r", "1qaz2wsx",
        "abcd1234", "abc12345", "aa123456", "asdf1234", "asdfghjk", "zxcvbnm1", "iloveyou",
        "admin123", "admin1234", "benfica1", "sporting", "portugal", "palavrapass", "sunshine",
        "football", "princess", "superman", "starwars", "minecraft", "fortnite", "valorant",
        "1234567890", "0123456789", "9876543210", "12345678910", "123456789a", "a123456789",
        "1234567890a", "123456789abc", "1q2w3e4r5t", "1q2w3e4r5t6y", "1qaz2wsx3edc", "q1w2e3r4t5",
        "qwertyuiop", "qwerty1234", "qwerty12345", "qwerty123456", "asdfghjkl1", "asdfghjkl123",
        "zxcvbnm123", "zxcvbnm1234", "123qweasdzxc", "qweasdzxc123", "abcdefghij", "abc1234567",
        "abcd123456", "password12", "password123", "password1234", "passw0rd123", "p@ssw0rd123",
        "palavrapasse", "palavra-passe", "palavrapasse1", "palavrapasse123", "administrador",
        "administrator", "admin12345", "admin123456", "administrador123", "iloveyou123",
        "welcome123", "welcome1234", "letmein123", "changeme123", "football123", "princess123",
        "sunshine123", "superman123", "batman12345", "dragon12345", "monkey12345", "starwars123",
        "benfica123", "benfica1904", "sporting123", "sporting1906", "porto12345", "fcporto123",
        "portugal123", "portugal2026", "lisboa12345", "escola12345", "aluno12345", "professor123",
        "estudante123", "computador123", "informatica123", "windows123", "windows10", "windows11",
        "minecraft123", "fortnite123", "valorant123", "teste12345", "test123456", "testtest12",
        "secret1234", "trustno1234", "master12345", "1234512345", "1122334455", "1212121212",
        "1231231234", "0987654321", "11223344556", "135792468a"
    };

    // Devolve o motivo da recusa, ou null se a palavra-passe for aceitável.
    // Só uma regra (além do mínimo de 8 caracteres): não pode ser uma palavra-passe comum.
    public static string? Motivo(string password) =>
        Comuns.Contains(password.Trim())
            ? "Essa palavra-passe é muito fácil de adivinhar. Usa o botão Gerar ou escolhe outra."
            : null;
}
