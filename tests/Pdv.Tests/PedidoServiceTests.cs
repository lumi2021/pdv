using Pdv.Core.Entities;
using Pdv.Core.Services;
using Pdv.Data.Services;

namespace Pdv.Tests;

public class PedidoServiceTests
{
    private static NovoPedido Pedido(int produtoId, int quantidade, string mesa = "Mesa 1") =>
        new(mesa, null, [new NovoItemPedido(produtoId, quantidade)]);

    [Fact]
    public async Task Criar_baixa_ingredientes_e_registra_movimentos()
    {
        using var db = new TestDb();
        var ids = db.Seed();
        var svc = new PedidoService(db);

        var pedido = await svc.CriarAsync(Pedido(ids.XBurger, 2, "Mesa 3"));

        Assert.Equal(1, pedido.Numero);
        Assert.Equal(StatusPedido.APreparar, pedido.Status);
        Assert.Equal(40m, pedido.Total);
        Assert.Equal("X-Burger", pedido.Itens.Single().NomeProduto);
        Assert.Equal(8m, db.Saldo(ids.Pao));
        Assert.Equal(700m, db.Saldo(ids.Carne));

        var movimentos = db.Movimentos();
        Assert.Equal(2, movimentos.Count);
        Assert.All(movimentos, m =>
        {
            Assert.Equal(TipoMovimentoEstoque.BaixaPedido, m.Tipo);
            Assert.Equal(pedido.Id, m.PedidoId);
            Assert.True(m.Quantidade < 0);
        });
    }

    [Fact]
    public async Task Criar_com_estoque_insuficiente_nao_altera_nada()
    {
        using var db = new TestDb();
        var ids = db.Seed(pao: 10, carne: 1000);
        var svc = new PedidoService(db);

        // 7 x 150 g = 1050 g > 1000 g. O pão (7 de 10) seria suficiente.
        var erro = await Assert.ThrowsAsync<EstoqueInsuficienteException>(
            () => svc.CriarAsync(Pedido(ids.XBurger, 7)));

        var falta = Assert.Single(erro.Faltas);
        Assert.Equal("Carne", falta.Ingrediente);
        Assert.Equal(50m, falta.Faltam);
        Assert.Equal(10m, db.Saldo(ids.Pao));
        Assert.Equal(1000m, db.Saldo(ids.Carne));
        Assert.Empty(db.Movimentos());
        Assert.Empty(await svc.ListarFilaAsync());
    }

    [Fact]
    public async Task Pedidos_simultaneos_nao_vendem_alem_do_estoque()
    {
        using var db = new TestDb();
        var ids = db.Seed(pao: 10, carne: 1000);
        var svc = new PedidoService(db);

        // Cada pedido usa 600 g de carne; só cabe um.
        var resultados = await Task.WhenAll(
            Tentar(svc.CriarAsync(Pedido(ids.XBurger, 4, "Mesa 1"))),
            Tentar(svc.CriarAsync(Pedido(ids.XBurger, 4, "Mesa 2"))));

        Assert.Equal(1, resultados.Count(ok => ok));
        Assert.Equal(400m, db.Saldo(ids.Carne));
        Assert.Equal(6m, db.Saldo(ids.Pao));

        static async Task<bool> Tentar(Task<Pedido> tarefa)
        {
            try { await tarefa; return true; }
            catch (EstoqueInsuficienteException) { return false; }
        }
    }

    [Fact]
    public async Task Numeros_dos_pedidos_sao_sequenciais()
    {
        using var db = new TestDb();
        var ids = db.Seed();
        var svc = new PedidoService(db);

        var p1 = await svc.CriarAsync(Pedido(ids.XBurger, 1));
        var p2 = await svc.CriarAsync(Pedido(ids.XBurger, 1));

        Assert.Equal([1, 2], new[] { p1.Numero, p2.Numero });
    }

    [Fact]
    public async Task Produto_inativo_ou_pedido_invalido_e_recusado()
    {
        using var db = new TestDb();
        var ids = db.Seed();
        var svc = new PedidoService(db);

        await Assert.ThrowsAsync<RegraDeNegocioException>(() => svc.CriarAsync(Pedido(ids.Descontinuado, 1)));
        await Assert.ThrowsAsync<RegraDeNegocioException>(() => svc.CriarAsync(Pedido(9999, 1)));
        await Assert.ThrowsAsync<RegraDeNegocioException>(() => svc.CriarAsync(Pedido(ids.XBurger, 0)));
        await Assert.ThrowsAsync<RegraDeNegocioException>(() => svc.CriarAsync(new NovoPedido("Mesa 1", null, [])));
        await Assert.ThrowsAsync<RegraDeNegocioException>(() => svc.CriarAsync(Pedido(ids.XBurger, 1, "  ")));
    }

    [Fact]
    public async Task Cancelar_devolve_ingredientes_e_registra_estorno()
    {
        using var db = new TestDb();
        var ids = db.Seed();
        var svc = new PedidoService(db);
        var pedido = await svc.CriarAsync(Pedido(ids.XBurger, 2));

        var cancelado = await svc.CancelarAsync(pedido.Id);

        Assert.Equal(StatusPedido.Cancelado, cancelado.Status);
        Assert.Equal(10m, db.Saldo(ids.Pao));
        Assert.Equal(1000m, db.Saldo(ids.Carne));
        Assert.Equal(2, db.Movimentos().Count(m => m.Tipo == TipoMovimentoEstoque.EstornoPedido));

        await Assert.ThrowsAsync<RegraDeNegocioException>(() => svc.CancelarAsync(pedido.Id));
    }

    [Fact]
    public async Task Cancelar_sem_devolver_mantem_a_baixa()
    {
        using var db = new TestDb();
        var ids = db.Seed();
        var svc = new PedidoService(db);
        var pedido = await svc.CriarAsync(Pedido(ids.XBurger, 2));

        await svc.CancelarAsync(pedido.Id, devolverEstoque: false);

        Assert.Equal(8m, db.Saldo(ids.Pao));
        Assert.Equal(700m, db.Saldo(ids.Carne));
        Assert.DoesNotContain(db.Movimentos(), m => m.Tipo == TipoMovimentoEstoque.EstornoPedido);
    }

    [Fact]
    public async Task Pedido_entregue_nao_pode_ser_cancelado()
    {
        using var db = new TestDb();
        var ids = db.Seed();
        var svc = new PedidoService(db);
        var pedido = await svc.CriarAsync(Pedido(ids.XBurger, 1));

        await svc.AlterarStatusAsync(pedido.Id, StatusPedido.EmPreparo);
        await svc.AlterarStatusAsync(pedido.Id, StatusPedido.Pronto);
        await svc.AlterarStatusAsync(pedido.Id, StatusPedido.Entregue);

        await Assert.ThrowsAsync<RegraDeNegocioException>(() => svc.CancelarAsync(pedido.Id));
    }

    [Fact]
    public async Task Status_segue_a_ordem_e_recusa_saltos()
    {
        using var db = new TestDb();
        var ids = db.Seed();
        var svc = new PedidoService(db);
        var pedido = await svc.CriarAsync(Pedido(ids.XBurger, 1));

        await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => svc.AlterarStatusAsync(pedido.Id, StatusPedido.Pronto));

        var emPreparo = await svc.AlterarStatusAsync(pedido.Id, StatusPedido.EmPreparo);
        Assert.Equal(StatusPedido.EmPreparo, emPreparo.Status);
        Assert.NotNull(emPreparo.AtualizadoEm);

        await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => svc.AlterarStatusAsync(pedido.Id, StatusPedido.APreparar));
    }

    [Fact]
    public async Task Fila_lista_so_pedidos_em_andamento_do_mais_antigo_ao_mais_novo()
    {
        using var db = new TestDb();
        var ids = db.Seed();
        var svc = new PedidoService(db);

        var p1 = await svc.CriarAsync(Pedido(ids.XBurger, 1, "Mesa 1"));
        var p2 = await svc.CriarAsync(Pedido(ids.XBurger, 1, "Mesa 2"));
        var p3 = await svc.CriarAsync(Pedido(ids.XBurger, 1, "Mesa 3"));
        var p4 = await svc.CriarAsync(Pedido(ids.XBurger, 1, "Mesa 4"));

        await svc.AlterarStatusAsync(p1.Id, StatusPedido.EmPreparo);
        await svc.AlterarStatusAsync(p1.Id, StatusPedido.Pronto);
        await svc.AlterarStatusAsync(p1.Id, StatusPedido.Entregue);
        await svc.CancelarAsync(p2.Id);
        await svc.AlterarStatusAsync(p3.Id, StatusPedido.EmPreparo);

        var fila = await svc.ListarFilaAsync();

        Assert.Equal([p3.Id, p4.Id], fila.Select(p => p.Id));
        Assert.All(fila, p => Assert.Single(p.Itens));
    }
}
