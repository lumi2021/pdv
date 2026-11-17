using Pdv.Core.Services;
using Pdv.Data.Services;

namespace Pdv.Tests;

public class MenuServiceTests
{
    private static ProdutoEdicao Novo(string nome, decimal preco, params ReceitaItemEdicao[] receita) =>
        new(null, nome, "Lanches", preco, receita);

    [Fact]
    public async Task Disponibilidade_e_limitada_pelo_ingrediente_mais_escasso()
    {
        using var db = new TestDb();
        db.Seed(pao: 10, carne: 1000); // 1000 g / 150 g = 6 hambúrgueres; pão daria 10
        var svc = new MenuService(db);

        var produto = Assert.Single(await svc.ListarAsync());

        Assert.Equal("X-Burger", produto.Nome);
        Assert.Equal(6, produto.Disponivel);
    }

    [Fact]
    public async Task Disponibilidade_acompanha_o_pao_quando_ele_e_o_limite()
    {
        using var db = new TestDb();
        db.Seed(pao: 3, carne: 1000);
        var svc = new MenuService(db);

        Assert.Equal(3, Assert.Single(await svc.ListarAsync()).Disponivel);
    }

    [Fact]
    public async Task Ingrediente_insuficiente_esgota_o_produto()
    {
        using var db = new TestDb();
        db.Seed(pao: 10, carne: 100); // menos de uma porção de 150 g
        var svc = new MenuService(db);

        Assert.Equal(0, Assert.Single(await svc.ListarAsync()).Disponivel);
    }

    [Fact]
    public async Task Produto_sem_receita_nao_tem_controle_de_estoque()
    {
        using var db = new TestDb();
        var svc = new MenuService(db);

        var refrigerante = await svc.SalvarAsync(Novo("Refrigerante", 6m));

        Assert.Null(refrigerante.Disponivel);
        Assert.Empty(refrigerante.Receita);
    }

    [Fact]
    public async Task Salvar_cria_produto_com_receita()
    {
        using var db = new TestDb();
        var ids = db.Seed();
        var svc = new MenuService(db);

        var salvo = await svc.SalvarAsync(Novo("  X-Salada ", 22.5m,
            new ReceitaItemEdicao(ids.Pao, 1),
            new ReceitaItemEdicao(ids.Carne, 100)));

        Assert.Equal("X-Salada", salvo.Nome);
        Assert.Equal(22.5m, salvo.Preco);
        Assert.Equal(2, salvo.Receita.Count);
        Assert.Equal(10, salvo.Disponivel); // pão 10/1 e carne 1000/100

        Assert.Contains(await svc.ListarAsync(), p => p.Id == salvo.Id);
    }

    [Fact]
    public async Task Salvar_existente_altera_dados_e_sincroniza_a_receita()
    {
        using var db = new TestDb();
        var ids = db.Seed();
        var svc = new MenuService(db);
        var criado = await svc.SalvarAsync(Novo("X-Salada", 20m,
            new ReceitaItemEdicao(ids.Pao, 1),
            new ReceitaItemEdicao(ids.Carne, 100)));

        // Tira a carne e dobra o pão.
        var semCarne = await svc.SalvarAsync(new ProdutoEdicao(
            criado.Id, "X-Salada Veg", "Veganos", 25m, [new ReceitaItemEdicao(ids.Pao, 2)]));

        Assert.Equal(criado.Id, semCarne.Id);
        Assert.Equal("X-Salada Veg", semCarne.Nome);
        Assert.Equal("Veganos", semCarne.Categoria);
        Assert.Equal(25m, semCarne.Preco);
        var item = Assert.Single(semCarne.Receita);
        Assert.Equal(ids.Pao, item.IngredienteId);
        Assert.Equal(2m, item.Quantidade);

        // Volta com a carne (mesmo par produto/ingrediente reaparece).
        var comCarne = await svc.SalvarAsync(new ProdutoEdicao(
            criado.Id, "X-Salada Veg", "Veganos", 25m,
            [new ReceitaItemEdicao(ids.Pao, 2), new ReceitaItemEdicao(ids.Carne, 50)]));

        Assert.Equal(2, comCarne.Receita.Count);
        Assert.Equal(2, (await svc.ListarAsync()).Count); // X-Burger e X-Salada Veg; nada duplicado
    }

    [Fact]
    public async Task Dados_invalidos_sao_recusados()
    {
        using var db = new TestDb();
        var ids = db.Seed();
        var svc = new MenuService(db);

        // nome repetido (sem diferenciar maiúsculas) e nome vazio
        await Assert.ThrowsAsync<RegraDeNegocioException>(() => svc.SalvarAsync(Novo("x-burger", 10m)));
        await Assert.ThrowsAsync<RegraDeNegocioException>(() => svc.SalvarAsync(Novo("  ", 10m)));
        // preço negativo
        await Assert.ThrowsAsync<RegraDeNegocioException>(() => svc.SalvarAsync(Novo("Suco", -1m)));
        // quantidade zero, ingrediente repetido, ingrediente inexistente
        await Assert.ThrowsAsync<RegraDeNegocioException>(() =>
            svc.SalvarAsync(Novo("Suco", 5m, new ReceitaItemEdicao(ids.Pao, 0))));
        await Assert.ThrowsAsync<RegraDeNegocioException>(() =>
            svc.SalvarAsync(Novo("Suco", 5m, new ReceitaItemEdicao(ids.Pao, 1), new ReceitaItemEdicao(ids.Pao, 2))));
        await Assert.ThrowsAsync<RegraDeNegocioException>(() =>
            svc.SalvarAsync(Novo("Suco", 5m, new ReceitaItemEdicao(9999, 1))));
        // produto a alterar não existe
        await Assert.ThrowsAsync<RegraDeNegocioException>(() =>
            svc.SalvarAsync(new ProdutoEdicao(9999, "Fantasma", "", 1m, [])));

        // Manter o próprio nome ao editar é permitido.
        var mantido = await svc.SalvarAsync(new ProdutoEdicao(ids.XBurger, "X-Burger", "Lanches", 21m, []));
        Assert.Equal(21m, mantido.Preco);
    }

    [Fact]
    public async Task Desativar_tira_da_lista_e_reativar_devolve()
    {
        using var db = new TestDb();
        var ids = db.Seed();
        var svc = new MenuService(db);

        await svc.DesativarAsync(ids.XBurger);

        Assert.Empty(await svc.ListarAsync());
        Assert.Contains(await svc.ListarAsync(incluirDesativados: true), p => p.Id == ids.XBurger && !p.Ativo);

        await svc.ReativarAsync(ids.XBurger);

        Assert.Equal(["X-Burger"], (await svc.ListarAsync()).Select(p => p.Nome));
        await Assert.ThrowsAsync<RegraDeNegocioException>(() => svc.DesativarAsync(9999));
    }
}
