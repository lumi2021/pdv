namespace Pdv.Core.Infra;

/// <summary>
/// Arquivo de registro (um por mês) para quem der suporte: o usuário vê uma mensagem simples e
/// os detalhes técnicos ficam aqui. Nunca lança: registrar um erro não pode causar outro.
/// </summary>
public static class RegistroDeErros
{
    private static readonly object Trava = new();
    private static string? _pasta;

    public static string? Pasta => _pasta;

    public static void Iniciar(string pasta) => _pasta = pasta;

    public static void Registrar(Exception ex, string? contexto = null)
    {
        var pasta = _pasta;
        if (pasta is null) return;

        try
        {
            lock (Trava)
            {
                Directory.CreateDirectory(pasta);
                var arquivo = Path.Combine(pasta, $"erros-{DateTime.Now:yyyy-MM}.log");
                var titulo = string.IsNullOrWhiteSpace(contexto) ? string.Empty : $" {contexto}";
                File.AppendAllText(arquivo, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]{titulo}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
            }
        }
        catch
        {
            // sem permissão ou disco cheio: não há onde registrar
        }
    }
}
