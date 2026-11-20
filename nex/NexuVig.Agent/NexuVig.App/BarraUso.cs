namespace NexuVig.App;

// Barra de utilização (CPU, RAM, disco): título à esquerda, valor à direita, barra por baixo.
// As cores seguem os mesmos limites do Dashboard (aviso > 75 %, perigo > 90 %).
internal sealed class BarraUso : Control
{
    private static readonly Color CorFundoBarra = Color.FromArgb(51, 65, 85);
    private static readonly Color CorNormal = Color.FromArgb(59, 130, 246);
    private static readonly Color CorAviso = Color.FromArgb(245, 158, 11);
    private static readonly Color CorPerigo = Color.FromArgb(239, 68, 68);

    private readonly string titulo;
    private double percentagem;
    private string valor = "—";

    public BarraUso(string titulo)
    {
        this.titulo = titulo;
        DoubleBuffered = true;
        Height = 46;
    }

    public void Definir(double novaPercentagem, string texto)
    {
        percentagem = Math.Clamp(novaPercentagem, 0, 100);
        valor = texto;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var g = e.Graphics;
        using var pincelTexto = new SolidBrush(ForeColor);

        g.DrawString(titulo, Font, pincelTexto, 0, 0);

        SizeF tamanho = g.MeasureString(valor, Font);
        g.DrawString(valor, Font, pincelTexto, Width - tamanho.Width, 0);

        int altura = Math.Max(6, Height / 6);
        int y = Height - altura - 2;

        using var pincelFundo = new SolidBrush(CorFundoBarra);
        g.FillRectangle(pincelFundo, 0, y, Width, altura);

        Color cor = percentagem > 90 ? CorPerigo : percentagem > 75 ? CorAviso : CorNormal;
        using var pincelBarra = new SolidBrush(cor);
        g.FillRectangle(pincelBarra, 0, y, (int)(Width * percentagem / 100), altura);
    }
}
