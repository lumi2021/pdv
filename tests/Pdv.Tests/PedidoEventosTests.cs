using Pdv.Core.Entities;
using Pdv.Core.Services;
using Pdv.Data.Services;

namespace Pdv.Tests;

public class PedidoEventosTests
{
    private static NovoPedido Pedido(TestDb.Ids ids, int quantidade = 1) =>
        new("Mesa 1", null, [new NovoItemPedido(ids.XBurger, quantidade)]);

    [Fact]
    public async Task Criar_avancar_e_cancelar_publicam_um_evento_cada()
    {
        using var db = new TestDb();
        var ids = db.Seed();
        var eventos = new PedidoEventos();
        var recebidos = new List<PedidoEvento>();
        eventos.Ocorreu += recebidos.Add;
        var servico = new PedidoService(db, eventos);

        var pedido = await servico.CriarAsync(Pedido(ids));
        await servico.AlterarStatusAsync(pedido.Id, StatusPedido.EmPreparo);
        await servico.CancelarAsync(pedido.Id);

        Assert.Collection(recebidos,
            e => { Assert.Equal(TipoEventoPedido.Criado, e.Tipo); Assert.Equal(StatusPedido.APreparar, e.Status); Assert.Equal(1, e.Numero); },
            e => { Assert.Equal(TipoEventoPedido.StatusAlterado, e.Tipo); Assert.Equal(StatusPedido.EmPreparo, e.Status); },
            e => { Assert.Equal(TipoEventoPedido.Cancelado, e.Tipo); Assert.Equal(StatusPedido.Cancelado, e.Status); });
        Assert.All(recebidos, e => Assert.Equal(pedido.Id, e.PedidoId));
    }

    [Fact]
    public async Task Operacao_que_falha_nao_publica_evento()
    {
        using var db = new TestDb();
        var ids = db.Seed(pao: 10, carne: 100);
        var eventos = new PedidoEventos();
        var recebidos = new List<PedidoEvento>();
        eventos.Ocorreu += recebidos.Add;
        var servico = new PedidoService(db, eventos);

        await Assert.ThrowsAsync<EstoqueInsuficienteException>(() => servico.CriarAsync(Pedido(ids, quantidade: 5)));

        Assert.Empty(recebidos);
    }

    [Fact]
    public async Task Assinante_com_defeito_nao_impede_o_pedido_nem_os_outros_assinantes()
    {
        using var db = new TestDb();
        var ids = db.Seed();
        var eventos = new PedidoEventos();
        var recebidos = new List<PedidoEvento>();
        eventos.Ocorreu += _ => throw new InvalidOperationException("tela quebrada");
        eventos.Ocorreu += recebidos.Add;
        var servico = new PedidoService(db, eventos);

        var pedido = await servico.CriarAsync(Pedido(ids));

        Assert.True(pedido.Id > 0);
        Assert.Single(recebidos);
        Assert.Single(await servico.ListarFilaAsync());
    }

    [Fact]
    public async Task Servico_sem_canal_de_eventos_continua_funcionando()
    {
        using var db = new TestDb();
        var ids = db.Seed();

        var pedido = await new PedidoService(db).CriarAsync(Pedido(ids));

        Assert.Equal(1, pedido.Numero);
    }
}
