namespace Pdv.Core.Entities;

public class Pedido
{
    public int Id { get; set; }

    /// <summary>Número sequencial amigável exibido na cozinha e no celular.</summary>
    public int Numero { get; set; }

    /// <summary>Mesa, comanda ou nome do cliente.</summary>
    public string Identificacao { get; set; } = string.Empty;

    public string? Observacao { get; set; }
    public StatusPedido Status { get; set; } = StatusPedido.APreparar;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime? AtualizadoEm { get; set; }

    public List<ItemPedido> Itens { get; set; } = [];

    public decimal Total => Itens.Sum(i => i.Subtotal);
}
