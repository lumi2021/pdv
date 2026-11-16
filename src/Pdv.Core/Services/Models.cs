namespace Pdv.Core.Services;

public sealed record NovoItemPedido(int ProdutoId, int Quantidade, string? Observacao = null);

public sealed record NovoPedido(
    string Identificacao,
    string? Observacao,
    IReadOnlyList<NovoItemPedido> Itens);
