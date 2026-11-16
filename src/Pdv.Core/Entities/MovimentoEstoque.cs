namespace Pdv.Core.Entities;

/// <summary>Histórico auditável de toda alteração de estoque.</summary>
public class MovimentoEstoque
{
    public int Id { get; set; }

    public int IngredienteId { get; set; }
    public Ingrediente Ingrediente { get; set; } = null!;

    public TipoMovimentoEstoque Tipo { get; set; }

    /// <summary>Com sinal: positivo entra, negativo sai.</summary>
    public decimal Quantidade { get; set; }

    public int? PedidoId { get; set; }
    public Pedido? Pedido { get; set; }

    public string? Observacao { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}
