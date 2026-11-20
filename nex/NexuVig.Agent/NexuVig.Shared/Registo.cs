namespace NexuVig.Shared
{
    // Pedido que o Agent faz ao Server na primeira vez (POST /api/registo).
    public class PedidoRegisto
    {
        public string AgentId { get; set; } = "";
        public string Nome { get; set; } = "";
    }

    // Resposta do Server: o token deste computador (só é mostrado esta vez).
    public class RespostaRegisto
    {
        public string Token { get; set; } = "";
        public bool Aprovado { get; set; }
    }

    // Resposta do Server ao envio de uma medição.
    public class RespostaMedicao
    {
        // "aprovado" ou "pendente"
        public string Estado { get; set; } = "";
        public string Mensagem { get; set; } = "";
    }
}
