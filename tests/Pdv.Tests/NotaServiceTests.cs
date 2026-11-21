using Microsoft.EntityFrameworkCore;
using Pdv.Core.Entities;
using Pdv.Core.Services;
using Pdv.Data.Services;

namespace Pdv.Tests;

public class NotaServiceTests
{
    private static async Task<Pedido> EntregarAsync(PedidoService pedidos, TestDb.Ids ids, string identificacao = "Mesa 1", int quantidade = 2)
    {
        var pedido = await pedidos.CriarAsync(new NovoPedido(identificacao, null, [new NovoItemPedido(ids.XBurger, quantidade, "sem cebola")]));
        await pedidos.AlterarStatusAsync(pedido.Id, StatusPedido.EmPreparo);
        await pedidos.AlterarStatusAsync(pedido.Id, StatusPedido.Pronto);
        return await pedidos.AlterarStatusAsync(pedido.Id, StatusPedido.Entregue);
    }

    [Fact]
    public async Task Entregar_gera_a_nota_com_a_copia_do_pedido()
    {
        using var db = new TestDb();
        var ids = db.Seed();
        var pedidos = new PedidoService(db);
        var notas = new NotaService(db);

        var pedido = await EntregarAsync(pedidos, ids);

        var nota = await notas.ObterPorPedidoAsync(pedido.Id);
        Assert.NotNull(nota);
        Assert.Equal(1, nota.Numero);
        Assert.Equal(40m, nota.Total);
        Assert.Equal("Mesa 1", nota.Conteudo.Identificacao);
        Assert.Equal(pedido.Numero, nota.Conteudo.NumeroDoPedido);
        var item = Assert.Single(nota.Conteudo.Itens);
        Assert.Equal(("X-Burger", 20m, 2, "sem cebola"), (item.Nome, item.PrecoUnitario, item.Quantidade, item.Observacao));
        Assert.Equal(40m, item.Subtotal);
    }

    [Fact]
    public async Task Pedido_que_nao_foi_entregue_ou_foi_cancelado_nao_tem_nota()
    {
        using var db = new TestDb();
        var ids = db.Seed();
        var pedidos = new PedidoService(db);
        var notas = new NotaService(db);

        var emAndamento = await pedidos.CriarAsync(new NovoPedido("Mesa 1", null, [new NovoItemPedido(ids.XBurger, 1)]));
        var cancelado = await pedidos.CriarAsync(new NovoPedido("Mesa 2", null, [new NovoItemPedido(ids.XBurger, 1)]));
        await pedidos.CancelarAsync(cancelado.Id);

        Assert.Empty(await notas.ListarAsync());
        await Assert.ThrowsAsync<RegraDeNegocioException>(() => notas.EmitirAsync(emAndamento.Id));
    }

    [Fact]
    public async Task Numeracao_das_notas_e_propria_e_sequencial()
    {
        using var db = new TestDb();
        var ids = db.Seed();
        var pedidos = new PedidoService(db);
        var notas = new NotaService(db);

        await pedidos.CriarAsync(new NovoPedido("Cancelado", null, [new NovoItemPedido(ids.XBurger, 1)])); // pedido 1, sem nota
        var p2 = await EntregarAsync(pedidos, ids, "Mesa 2", 1);
        var p3 = await EntregarAsync(pedidos, ids, "Mesa 3", 1);

        Assert.Equal(1, (await notas.ObterPorPedidoAsync(p2.Id))!.Numero);
        Assert.Equal(2, (await notas.ObterPorPedidoAsync(p3.Id))!.Numero);
        Assert.Equal(new[] { 2, 1 }, (await notas.ListarAsync()).Select(n => n.Numero)); // mais nova primeiro
    }

    [Fact]
    public async Task Nota_nao_muda_quando_preco_ou_nome_do_produto_mudam_depois()
    {
        using var db = new TestDb();
        var ids = db.Seed();
        var pedido = await EntregarAsync(new PedidoService(db), ids);

        using (var ctx = db.CreateDbContext())
        {
            var produto = await ctx.Produtos.SingleAsync(p => p.Id == ids.XBurger);
            produto.Preco = 99m;
            produto.Nome = "Burger Premium";
            await ctx.SaveChangesAsync();
        }

        var nota = (await new NotaService(db).ObterPorPedidoAsync(pedido.Id))!;
        Assert.Equal(40m, nota.Total);
        Assert.Equal("X-Burger", nota.Conteudo.Itens[0].Nome);
        Assert.Equal(20m, nota.Conteudo.Itens[0].PrecoUnitario);
    }

    [Fact]
    public async Task Emitir_nao_duplica_e_devolve_a_nota_existente()
    {
        using var db = new TestDb();
        var ids = db.Seed();
        var pedido = await EntregarAsync(new PedidoService(db), ids);
        var notas = new NotaService(db);

        var segunda = await notas.EmitirAsync(pedido.Id);

        Assert.Equal(1, segunda.Numero);
        Assert.Single(await notas.ListarAsync());
    }

    [Fact]
    public async Task Emitir_pendentes_cria_as_notas_de_pedidos_entregues_antes_da_nota_existir()
    {
        using var db = new TestDb();
        var ids = db.Seed();
        var pedidos = new PedidoService(db);
        var notas = new NotaService(db);
        var antigo = await EntregarAsync(pedidos, ids, "Mesa antiga", 1);

        // Simula um banco de antes do passo 10: pedido entregue sem nota.
        using (var ctx = db.CreateDbContext())
        {
            ctx.NotasInternas.RemoveRange(ctx.NotasInternas);
            await ctx.SaveChangesAsync();
        }

        Assert.Equal(1, await notas.EmitirPendentesAsync());
        Assert.Equal(0, await notas.EmitirPendentesAsync()); // segunda vez não faz nada
        Assert.Equal("Mesa antiga", (await notas.ObterPorPedidoAsync(antigo.Id))!.Conteudo.Identificacao);
    }

    [Fact]
    public async Task Busca_por_numero_da_nota_do_pedido_ou_por_mesa_sem_diferenciar_maiusculas()
    {
        using var db = new TestDb();
        var ids = db.Seed();
        var pedidos = new PedidoService(db);
        var notas = new NotaService(db);
        await EntregarAsync(pedidos, ids, "Mesa Varanda", 1);
        await EntregarAsync(pedidos, ids, "Balcão", 1);

        Assert.Equal("Mesa Varanda", Assert.Single(await notas.ListarAsync("varanda")).Identificacao);
        Assert.Equal("Balcão", Assert.Single(await notas.ListarAsync("#2")).Identificacao);
        Assert.Empty(await notas.ListarAsync("%"));          // % é texto comum, não curinga
        Assert.Equal(2, (await notas.ListarAsync("  ")).Count);
        Assert.Single(await notas.ListarAsync(limite: 1));
    }

    [Fact]
    public async Task Filtro_de_data_traz_so_notas_emitidas_a_partir_dela()
    {
        using var db = new TestDb();
        var ids = db.Seed();
        var notas = new NotaService(db);
        await EntregarAsync(new PedidoService(db), ids);

        Assert.Single(await notas.ListarAsync(desdeUtc: DateTime.UtcNow.AddHours(-1)));
        Assert.Empty(await notas.ListarAsync(desdeUtc: DateTime.UtcNow.AddHours(1)));
    }
}
