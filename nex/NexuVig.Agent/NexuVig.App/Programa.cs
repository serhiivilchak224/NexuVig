namespace NexuVig.App;

internal static class Programa
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new JanelaPrincipal());
    }
}
