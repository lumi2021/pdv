using Pdv.Core.Entities;

namespace Pdv.Core.Services;

public interface IPedidoService
{
    /// <summary>
    /// Cria o pedido e desconta os ingredientes do estoque de forma atômica:
    /// ou tudo é gravado, ou nada. Lança <see cref="EstoqueInsuficienteException"/>
    /// se faltar qualquer ingrediente.
    /// </summary>
    Task<Pedido> CriarAsync(NovoPedido novo, CancellationToken ct = default);

    /// <summary>Avança o pedido na ordem: a preparar → em preparo → pronto → entregue.</summary>
    Task<Pedido> AlterarStatusAsync(int pedidoId, StatusPedido novoStatus, CancellationToken ct = default);

    /// <summary>
    /// Cancela o pedido. Com <paramref name="devolverEstoque"/> = true, os ingredientes voltam ao estoque
    /// (use false quando a comida já foi preparada e os ingredientes foram perdidos).
    /// </summary>
    Task<Pedido> CancelarAsync(int pedidoId, bool devolverEstoque = true, CancellationToken ct = default);

    /// <summary>Pedidos em andamento (a preparar, em preparo, prontos), do mais antigo ao mais novo.</summary>
    Task<IReadOnlyList<Pedido>> ListarFilaAsync(CancellationToken ct = default);
}
