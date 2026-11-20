using System.Net.Http.Json;
using System.Security.Claims;
using NexuVig.Shared;

namespace NexuVig.Dashboard;

// Dados de quem entrou, usados para registar o autor das avarias e intervenções.
public static class Sessao
{
    public static bool EAdministrador(ClaimsPrincipal utilizador) =>
        utilizador.IsInRole(Perfis.Administrador);

    // Ex.: "Professor João Silva (joao)"
    public static string Autor(ClaimsPrincipal utilizador)
    {
        string conta = utilizador.Identity?.Name ?? "";
        string nome = utilizador.FindFirst("nome")?.Value ?? "";

        if (string.IsNullOrWhiteSpace(nome) || nome == conta) return conta;
        return string.IsNullOrWhiteSpace(conta) ? nome : $"{nome} ({conta})";
    }

    // O Server responde com { "mensagem": "..." } nos erros.
    public static async Task<string> MensagemErro(HttpResponseMessage resposta)
    {
        try
        {
            var corpo = await resposta.Content.ReadFromJsonAsync<RespostaErro>();
            if (!string.IsNullOrWhiteSpace(corpo?.Mensagem)) return corpo.Mensagem;
        }
        catch
        {
            // Resposta sem JSON: usa a mensagem genérica.
        }

        return $"O Server recusou o pedido (HTTP {(int)resposta.StatusCode}).";
    }

    private sealed class RespostaErro
    {
        public string? Mensagem { get; set; }
    }
}
