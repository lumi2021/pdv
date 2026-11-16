using Pdv.Core.Entities;
using Pdv.Core.Services;
using Pdv.Data.Services;

namespace Pdv.Tests;

public class EstoqueServiceTests
{
    [Fact]
    public async Task Entrada_soma_ao_saldo_e_registra_movimento()
    {
        using var db = new TestDb();
        var ids = db.Seed();
        var svc = new EstoqueService(db);

        var ingrediente = await svc.RegistrarEntradaAsync(ids.Pao, 5, "Compra do dia");

        Assert.Equal(15m, ingrediente.QuantidadeAtual);
        Assert.Equal(15m, db.Saldo(ids.Pao));

        var movimento = Assert.Single(db.Movimentos());
        Assert.Equal(TipoMovimentoEstoque.Entrada, movimento.Tipo);
        Assert.Equal(5m, movimento.Quantidade);
        Assert.Equal("Compra do dia", movimento.Observacao);
    }

    [Fact]
    public async Task Entrada_invalida_ou_ingrediente_inexistente_e_recusada()
    {
        using var db = new TestDb();
        var ids = db.Seed();
        var svc = new EstoqueService(db);

        await Assert.ThrowsAsync<RegraDeNegocioException>(() => svc.RegistrarEntradaAsync(ids.Pao, 0));
        await Assert.ThrowsAsync<RegraDeNegocioException>(() => svc.RegistrarEntradaAsync(ids.Pao, -3));
        await Assert.ThrowsAsync<RegraDeNegocioException>(() => svc.RegistrarEntradaAsync(9999, 1));
    }

    [Fact]
    public async Task Ajuste_define_o_saldo_e_registra_a_diferenca()
    {
        using var db = new TestDb();
        var ids = db.Seed(carne: 1000);
        var svc = new EstoqueService(db);

        await svc.AjustarSaldoAsync(ids.Carne, 900, "Contagem física");

        Assert.Equal(900m, db.Saldo(ids.Carne));

        var movimento = Assert.Single(db.Movimentos());
        Assert.Equal(TipoMovimentoEstoque.Ajuste, movimento.Tipo);
        Assert.Equal(-100m, movimento.Quantidade);

        // Mesmo saldo: nada a registrar.
        await svc.AjustarSaldoAsync(ids.Carne, 900);
        Assert.Single(db.Movimentos());

        await Assert.ThrowsAsync<RegraDeNegocioException>(() => svc.AjustarSaldoAsync(ids.Carne, -1));
    }

    [Fact]
    public async Task Estoque_baixo_inclui_saldo_igual_ou_abaixo_do_minimo()
    {
        using var db = new TestDb();
        db.Seed(pao: 2, carne: 1000); // pão no mínimo (2), carne bem acima (mín. 200)
        var svc = new EstoqueService(db);

        var baixos = await svc.ListarEstoqueBaixoAsync();

        Assert.Equal(["Pão"], baixos.Select(i => i.Nome));
    }

    [Fact]
    public async Task Criar_ingrediente_com_saldo_inicial_registra_entrada()
    {
        using var db = new TestDb();
        var svc = new EstoqueService(db);

        var alface = await svc.CriarIngredienteAsync("  Alface ", UnidadeMedida.Unidade, 5, saldoInicial: 20);

        Assert.Equal("Alface", alface.Nome);
        Assert.Equal(20m, db.Saldo(alface.Id));

        var movimento = Assert.Single(db.Movimentos());
        Assert.Equal(TipoMovimentoEstoque.Entrada, movimento.Tipo);
        Assert.Equal(20m, movimento.Quantidade);
    }

    [Fact]
    public async Task Nome_repetido_ou_invalido_e_recusado()
    {
        using var db = new TestDb();
        var ids = db.Seed();
        var svc = new EstoqueService(db);

        await Assert.ThrowsAsync<RegraDeNegocioException>(() => svc.CriarIngredienteAsync("pão", UnidadeMedida.Unidade, 0));
        await Assert.ThrowsAsync<RegraDeNegocioException>(() => svc.CriarIngredienteAsync("  ", UnidadeMedida.Unidade, 0));
        await Assert.ThrowsAsync<RegraDeNegocioException>(() => svc.CriarIngredienteAsync("Queijo", UnidadeMedida.Grama, -1));
        await Assert.ThrowsAsync<RegraDeNegocioException>(() => svc.AtualizarIngredienteAsync(ids.Carne, "Pão", 200));

        // Manter o próprio nome (mudando só o mínimo) é permitido.
        var atualizado = await svc.AtualizarIngredienteAsync(ids.Carne, "Carne", 300);
        Assert.Equal(300m, atualizado.EstoqueMinimo);
    }

    [Fact]
    public async Task Desativar_e_bloqueado_se_usado_em_receita_de_produto_ativo()
    {
        using var db = new TestDb();
        var ids = db.Seed();
        var svc = new EstoqueService(db);

        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(() => svc.DesativarAsync(ids.Pao));

        Assert.Contains("X-Burger", erro.Message);
    }

    [Fact]
    public async Task Desativar_esconde_da_lista_e_reativar_traz_de_volta()
    {
        using var db = new TestDb();
        db.Seed();
        var svc = new EstoqueService(db);
        var alface = await svc.CriarIngredienteAsync("Alface", UnidadeMedida.Unidade, 5, 20);

        await svc.DesativarAsync(alface.Id);

        Assert.DoesNotContain(await svc.ListarAsync(), i => i.Id == alface.Id);
        Assert.Contains(await svc.ListarAsync(incluirDesativados: true), i => i.Id == alface.Id);

        await svc.ReativarAsync(alface.Id);

        Assert.Contains(await svc.ListarAsync(), i => i.Id == alface.Id);
    }
}
