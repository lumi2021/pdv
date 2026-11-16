namespace Pdv.Core.Entities;

/// <summary>Nota interna (sem valor fiscal) gerada ao fechar um pedido.</summary>
public class NotaInterna
{
    public int Id { get; set; }

    public int PedidoId { get; set; }
    public Pedido Pedido { get; set; } = null!;

    public int Numero { get; set; }
    public DateTime EmitidaEm { get; set; } = DateTime.UtcNow;
    public decimal Total { get; set; }

    /// <summary>Cópia do conteúdo (itens e preços) em JSON, imutável após a emissão.</summary>
    public string ConteudoJson { get; set; } = "{}";
}
