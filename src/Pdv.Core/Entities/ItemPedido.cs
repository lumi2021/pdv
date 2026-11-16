namespace Pdv.Core.Entities;

public class ItemPedido
{
    public int Id { get; set; }

    public int PedidoId { get; set; }
    public Pedido Pedido { get; set; } = null!;

    public int ProdutoId { get; set; }
    public Produto Produto { get; set; } = null!;

    // Snapshot: preserva nome e preço do momento da venda, mesmo se o menu mudar depois.
    public string NomeProduto { get; set; } = string.Empty;
    public decimal PrecoUnitario { get; set; }

    public int Quantidade { get; set; }
    public string? Observacao { get; set; }

    public decimal Subtotal => PrecoUnitario * Quantidade;
}
