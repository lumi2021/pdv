using Pdv.Core.Entities;

namespace Pdv.Core.Services;

/// <summary>Cardápio e estoque de exemplo, para quem quer ver o sistema funcionando antes de cadastrar o próprio.</summary>
public static class DadosDeExemplo
{
    /// <summary>Cria ingredientes e produtos de exemplo. Devolve quantos produtos foram criados.</summary>
    public static async Task<int> CriarAsync(IEstoqueService estoque, IMenuService menu, CancellationToken ct = default)
    {
        var pao = await estoque.CriarIngredienteAsync("Pão de hambúrguer", UnidadeMedida.Unidade, 10, 40, ct);
        var carne = await estoque.CriarIngredienteAsync("Carne (hambúrguer)", UnidadeMedida.Grama, 1000, 6000, ct);
        var queijo = await estoque.CriarIngredienteAsync("Queijo fatiado", UnidadeMedida.Unidade, 10, 60, ct);
        var batata = await estoque.CriarIngredienteAsync("Batata", UnidadeMedida.Grama, 2000, 10000, ct);
        var refri = await estoque.CriarIngredienteAsync("Refrigerante lata", UnidadeMedida.Unidade, 6, 36, ct);

        ProdutoEdicao[] produtos =
        [
            new(null, "Hambúrguer simples", "Lanches", 18m, [new(pao.Id, 1), new(carne.Id, 150)]),
            new(null, "X-Burger", "Lanches", 22m, [new(pao.Id, 1), new(carne.Id, 150), new(queijo.Id, 1)]),
            new(null, "Batata frita", "Porções", 15m, [new(batata.Id, 200)]),
            new(null, "Refrigerante lata", "Bebidas", 6m, [new(refri.Id, 1)])
        ];

        foreach (var produto in produtos)
            await menu.SalvarAsync(produto, ct);

        return produtos.Length;
    }
}
