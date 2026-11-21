namespace Pdv.Core.Services;

public interface INotaService
{
    /// <summary>
    /// Notas emitidas, da mais nova para a mais antiga. <paramref name="busca"/> procura no número da nota,
    /// no número do pedido e na mesa/cliente. <paramref name="desdeUtc"/> limita pela data de emissão.
    /// </summary>
    Task<IReadOnlyList<NotaResumo>> ListarAsync(
        string? busca = null, DateTime? desdeUtc = null, int limite = 200, CancellationToken ct = default);

    Task<NotaDetalhe?> ObterAsync(int notaId, CancellationToken ct = default);

    Task<NotaDetalhe?> ObterPorPedidoAsync(int pedidoId, CancellationToken ct = default);

    /// <summary>
    /// Emite a nota de um pedido já entregue. Se ela já existir, devolve a existente (nunca duplica).
    /// Normalmente a nota nasce sozinha na entrega; isto serve para pedidos entregues antes da nota existir.
    /// </summary>
    Task<NotaDetalhe> EmitirAsync(int pedidoId, CancellationToken ct = default);

    /// <summary>Emite as notas que faltam para pedidos já entregues. Devolve quantas criou.</summary>
    Task<int> EmitirPendentesAsync(CancellationToken ct = default);
}
