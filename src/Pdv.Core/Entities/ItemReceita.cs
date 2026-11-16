namespace Pdv.Core.Entities;

/// <summary>Quanto de um ingrediente é consumido por 1 unidade de um produto.</summary>
public class ItemReceita
{
    public int Id { get; set; }

    public int ProdutoId { get; set; }
    public Produto Produto { get; set; } = null!;

    public int IngredienteId { get; set; }
    public Ingrediente Ingrediente { get; set; } = null!;

    public decimal Quantidade { get; set; }
}
