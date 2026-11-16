using Microsoft.EntityFrameworkCore;
using Pdv.Core.Entities;
using Pdv.Core.Services;

namespace Pdv.Data.Services;

public sealed class EstoqueService(IDbContextFactory<PdvDbContext> factory) : IEstoqueService
{
    public async Task<IReadOnlyList<Ingrediente>> ListarAsync(bool incluirDesativados = false, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.Ingredientes
            .AsNoTracking()
            .Where(i => incluirDesativados || i.Ativo)
            .OrderBy(i => i.Nome)
            .ToListAsync(ct);
    }

    public async Task<Ingrediente> CriarIngredienteAsync(
        string nome, UnidadeMedida unidade, decimal estoqueMinimo, decimal saldoInicial = 0, CancellationToken ct = default)
    {
        var nomeLimpo = ValidarNomeEMinimo(nome, estoqueMinimo);
        if (saldoInicial < 0)
            throw new RegraDeNegocioException("O saldo inicial não pode ser negativo.");

        using var trava = await EscritaSerializada.EntrarAsync(ct);
        await using var db = await factory.CreateDbContextAsync(ct);

        await GarantirNomeLivreAsync(db, nomeLimpo, ingredienteId: 0, ct);

        var ingrediente = new Ingrediente
        {
            Nome = nomeLimpo,
            Unidade = unidade,
            EstoqueMinimo = estoqueMinimo,
            QuantidadeAtual = saldoInicial
        };
        db.Ingredientes.Add(ingrediente);

        if (saldoInicial > 0)
        {
            db.MovimentosEstoque.Add(new MovimentoEstoque
            {
                Ingrediente = ingrediente,
                Tipo = TipoMovimentoEstoque.Entrada,
                Quantidade = saldoInicial,
                Observacao = "Saldo inicial"
            });
        }

        await db.SaveChangesAsync(ct);
        return ingrediente;
    }

    public async Task<Ingrediente> AtualizarIngredienteAsync(
        int ingredienteId, string nome, decimal estoqueMinimo, CancellationToken ct = default)
    {
        var nomeLimpo = ValidarNomeEMinimo(nome, estoqueMinimo);

        using var trava = await EscritaSerializada.EntrarAsync(ct);
        await using var db = await factory.CreateDbContextAsync(ct);

        var ingrediente = await CarregarAsync(db, ingredienteId, ct);
        await GarantirNomeLivreAsync(db, nomeLimpo, ingredienteId, ct);

        ingrediente.Nome = nomeLimpo;
        ingrediente.EstoqueMinimo = estoqueMinimo;

        await db.SaveChangesAsync(ct);
        return ingrediente;
    }

    public async Task DesativarAsync(int ingredienteId, CancellationToken ct = default)
    {
        using var trava = await EscritaSerializada.EntrarAsync(ct);
        await using var db = await factory.CreateDbContextAsync(ct);

        var ingrediente = await CarregarAsync(db, ingredienteId, ct);

        var produtosEmUso = await db.ItensReceita
            .Where(r => r.IngredienteId == ingredienteId && r.Produto.Ativo)
            .Select(r => r.Produto.Nome)
            .Distinct()
            .ToListAsync(ct);

        if (produtosEmUso.Count > 0)
            throw new RegraDeNegocioException(
                $"Este ingrediente é usado nas receitas de: {string.Join(", ", produtosEmUso)}. " +
                "Tire-o dessas receitas antes de desativar.");

        ingrediente.Ativo = false;
        await db.SaveChangesAsync(ct);
    }

    public async Task ReativarAsync(int ingredienteId, CancellationToken ct = default)
    {
        using var trava = await EscritaSerializada.EntrarAsync(ct);
        await using var db = await factory.CreateDbContextAsync(ct);

        var ingrediente = await CarregarAsync(db, ingredienteId, ct);
        ingrediente.Ativo = true;
        await db.SaveChangesAsync(ct);
    }

    public async Task<Ingrediente> RegistrarEntradaAsync(
        int ingredienteId, decimal quantidade, string? observacao = null, CancellationToken ct = default)
    {
        if (quantidade <= 0)
            throw new RegraDeNegocioException("A quantidade de entrada deve ser maior que zero.");

        using var trava = await EscritaSerializada.EntrarAsync(ct);
        await using var db = await factory.CreateDbContextAsync(ct);

        var ingrediente = await CarregarAsync(db, ingredienteId, ct);
        ingrediente.QuantidadeAtual += quantidade;

        db.MovimentosEstoque.Add(new MovimentoEstoque
        {
            Ingrediente = ingrediente,
            Tipo = TipoMovimentoEstoque.Entrada,
            Quantidade = quantidade,
            Observacao = observacao
        });

        await db.SaveChangesAsync(ct);
        return ingrediente;
    }

    public async Task<Ingrediente> AjustarSaldoAsync(
        int ingredienteId, decimal novoSaldo, string? observacao = null, CancellationToken ct = default)
    {
        if (novoSaldo < 0)
            throw new RegraDeNegocioException("O saldo não pode ser negativo.");

        using var trava = await EscritaSerializada.EntrarAsync(ct);
        await using var db = await factory.CreateDbContextAsync(ct);

        var ingrediente = await CarregarAsync(db, ingredienteId, ct);
        var diferenca = novoSaldo - ingrediente.QuantidadeAtual;
        if (diferenca == 0) return ingrediente;

        ingrediente.QuantidadeAtual = novoSaldo;
        db.MovimentosEstoque.Add(new MovimentoEstoque
        {
            Ingrediente = ingrediente,
            Tipo = TipoMovimentoEstoque.Ajuste,
            Quantidade = diferenca,
            Observacao = observacao
        });

        await db.SaveChangesAsync(ct);
        return ingrediente;
    }

    public async Task<IReadOnlyList<Ingrediente>> ListarEstoqueBaixoAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var ativos = await db.Ingredientes
            .AsNoTracking()
            .Where(i => i.Ativo)
            .OrderBy(i => i.Nome)
            .ToListAsync(ct);

        // Comparação de decimal feita em memória: o SQLite guarda decimal como texto.
        return ativos.Where(i => i.EstoqueBaixo).ToList();
    }

    private static string ValidarNomeEMinimo(string nome, decimal estoqueMinimo)
    {
        var limpo = (nome ?? string.Empty).Trim();
        if (limpo.Length == 0)
            throw new RegraDeNegocioException("Informe o nome do ingrediente.");
        if (limpo.Length > 120)
            throw new RegraDeNegocioException("O nome do ingrediente é muito longo (máximo de 120 letras).");
        if (estoqueMinimo < 0)
            throw new RegraDeNegocioException("O estoque mínimo não pode ser negativo.");
        return limpo;
    }

    private static async Task GarantirNomeLivreAsync(PdvDbContext db, string nome, int ingredienteId, CancellationToken ct)
    {
        var minusculo = nome.ToLower();
        var existe = await db.Ingredientes.AnyAsync(i => i.Id != ingredienteId && i.Nome.ToLower() == minusculo, ct);
        if (existe)
            throw new RegraDeNegocioException($"Já existe um ingrediente chamado \"{nome}\" (ele pode estar desativado).");
    }

    private static async Task<Ingrediente> CarregarAsync(PdvDbContext db, int id, CancellationToken ct) =>
        await db.Ingredientes.FirstOrDefaultAsync(i => i.Id == id, ct)
        ?? throw new RegraDeNegocioException("Ingrediente não encontrado.");
}
