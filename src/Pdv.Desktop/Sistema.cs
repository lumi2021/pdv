using System.Diagnostics;

namespace Pdv.Desktop;

internal static class Sistema
{
    /// <summary>Abre uma pasta (ou arquivo) no programa padrão do sistema. Cria a pasta se ainda não existir.</summary>
    public static bool Abrir(string caminho)
    {
        try
        {
            if (!File.Exists(caminho))
                Directory.CreateDirectory(caminho);

            Process.Start(new ProcessStartInfo(caminho) { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
