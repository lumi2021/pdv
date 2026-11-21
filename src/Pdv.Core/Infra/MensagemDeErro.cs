using Pdv.Core.Services;

namespace Pdv.Core.Infra;

/// <summary>Transforma exceções técnicas em frases que o dono da lanchonete entende.</summary>
public static class MensagemDeErro
{
    /// <summary>
    /// Mensagem para mostrar ao usuário. Regras de negócio já vêm em português simples e passam direto.
    /// Qualquer outra falha é gravada no registro de erros.
    /// </summary>
    /// <param name="acao">O que se tentava fazer, no infinitivo: "salvar o produto", "ler os pedidos".</param>
    public static string Para(Exception ex, string acao = "concluir esta ação")
    {
        if (ex is RegraDeNegocioException)
            return ex.Message;

        if (ex is OperationCanceledException)
            return $"Não deu tempo de {acao}. Tente de novo.";

        RegistroDeErros.Registrar(ex, $"Ao {acao}");

        for (var atual = ex; atual is not null; atual = atual.InnerException)
        {
            if (atual is RegraDeNegocioException regra)
                return regra.Message;

            if (CodigoDoSqlite(atual) is { } codigo)
            {
                return codigo switch
                {
                    5 or 6 => "O banco de dados está ocupado neste momento. Espere alguns segundos e tente de novo.",
                    13 => "O disco do computador está cheio. Libere espaço e tente de novo.",
                    8 => "Não é possível gravar no arquivo de dados. Verifique se a pasta não está protegida contra escrita.",
                    10 or 11 or 26 => "O arquivo de dados parece danificado. Feche o programa e abra de novo. " +
                                      "Se continuar, chame quem instalou o sistema: há cópias de segurança guardadas.",
                    _ => Generica(acao)
                };
            }

            if (atual is IOException io && (io.HResult & 0xFFFF) is 0x27 or 0x70)
                return "O disco do computador está cheio. Libere espaço e tente de novo.";

            if (atual is UnauthorizedAccessException)
                return "O sistema não tem permissão para acessar a pasta dos dados. Chame quem instalou o programa.";
        }

        return Generica(acao);
    }

    private static string Generica(string acao) =>
        $"Algo inesperado aconteceu ao {acao}. Tente de novo. Se repetir, chame quem instalou o sistema " +
        "e informe o arquivo de registro (Ajustes > Abrir pasta de registros).";

    // O Pdv.Core não conhece o SQLite: lê o código de erro pela própria exceção.
    private static int? CodigoDoSqlite(Exception ex) =>
        ex.GetType().Name == "SqliteException"
            ? ex.GetType().GetProperty("SqliteErrorCode")?.GetValue(ex) as int?
            : null;
}
