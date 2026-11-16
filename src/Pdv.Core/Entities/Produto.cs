namespace Pdv.Core.Entities;

public class Produto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public decimal Preco { get; set; }
    public bool Ativo { get; set; } = true;

    /// <summary>Ingredientes consumidos por unidade vendida.</summary>
    public List<ItemReceita> Receita { get; set; } = [];
}
