using Pdv.Core.Services;
using Pdv.Data.Services;

namespace Pdv.Tests;

public class DadosDeExemploTests
{
    [Fact]
    public async Task Exemplo_cria_cardapio_vendavel_com_estoque_e_receitas()
    {
        using var db = new TestDb();
        var estoque = new EstoqueService(db);
        var menu = new MenuService(db);

        var criados = await DadosDeExemplo.CriarAsync(estoque, menu);

        Assert.Equal(4, criados);
        Assert.Equal(5, (await estoque.ListarAsync()).Count);

        var produtos = await menu.ListarAsync();
        Assert.Equal(4, produtos.Count);
        Assert.All(produtos, p => Assert.True(p.Disponivel > 0, $"{p.Nome} deveria poder ser vendido"));
        Assert.Empty(await estoque.ListarEstoqueBaixoAsync());
    }

    [Fact]
    public async Task Exemplo_permite_fazer_um_pedido_de_verdade()
    {
        using var db = new TestDb();
        await DadosDeExemplo.CriarAsync(new EstoqueService(db), new MenuService(db));
        var xBurger = (await new MenuService(db).ListarAsync()).Single(p => p.Nome == "X-Burger");

        var pedido = await new PedidoService(db).CriarAsync(new NovoPedido("Mesa 1", null, [new NovoItemPedido(xBurger.Id, 2)]));

        Assert.Equal(44m, pedido.Total);
    }
}
