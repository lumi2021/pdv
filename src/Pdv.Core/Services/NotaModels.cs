namespace Pdv.Core.Services;

/// <summary>Texto que acompanha toda nota, na tela e no PDF.</summary>
public static class NotaAviso
{
    public const string SemValorFiscal = "Documento interno de controle. Sem valor fiscal.";
}

public sealed record NotaItem(string Nome, decimal PrecoUnitario, int Quantidade, string? Observacao)
{
    public decimal Subtotal => PrecoUnitario * Quantidade;
}

/// <summary>O que a nota guarda: cópia do pedido no momento da entrega. Não muda se o cardápio mudar depois.</summary>
public sealed record NotaConteudo(
    int NumeroDoPedido,
    string Identificacao,
    string? Observacao,
    DateTime PedidoCriadoEmUtc,
    IReadOnlyList<NotaItem> Itens);

public sealed record NotaDetalhe(int Id, int Numero, DateTime EmitidaEmUtc, decimal Total, NotaConteudo Conteudo);

public sealed record NotaResumo(int Id, int Numero, int NumeroDoPedido, string Identificacao, DateTime EmitidaEmUtc, decimal Total);
