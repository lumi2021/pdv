using Microsoft.EntityFrameworkCore;
using Pdv.Core.Entities;
using Pdv.Core.Services;

namespace Pdv.Data.Services;

public sealed class MenuService(IDbContextFactory<PdvDbContext> factory) : IMenuService
{
    public async Task<IReadOnlyList<ProdutoMenuDto>> ListarAsync(bool incluirDesativados = false, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var produtos = await db.Produtos
            .AsNoTracking()
            .Include(p => p.Receita).ThenInclude(r => r.Ingrediente)
            .Where(p => incluirDesativados || p.Ativo)
            .OrderBy(p => p.Categoria).ThenBy(p => p.Nome)
            .ToListAsync(ct);

        return produtos.Select(ParaDto).ToList();
    }

    public async Task<ProdutoMenuDto> SalvarAsync(ProdutoEdicao edicao, CancellationToken ct = default)
    {
        var (nome, categoria) = Validar(edicao);
        var idsIngredientes = edicao.Receita.Select(r => r.IngredienteId).ToList();

        using var trava = await EscritaSerializada.EntrarAsync(ct);
        await using var db = await factory.CreateDbContextAsync(ct);

        var ingredientes = await db.Ingredientes
            .Where(i => idsIngredientes.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, ct);

        if (idsIngredientes.Any(id => !ingredientes.TryGetValue(id, out var ing) || !ing.Ativo))
            throw new RegraDeNegocioException("Um dos ingredientes da receita não existe ou está desativado.");

        var idDoProduto = edicao.Id ?? 0;
        var minusculo = nome.ToLower();
        if (await db.Produtos.AnyAsync(p => p.Id != idDoProduto && p.Nome.ToLower() == minusculo, ct))
            throw new RegraDeNegocioException($"Já existe um produto chamado \"{nome}\" (ele pode estar desativado).");

        Produto produto;
        if (edicao.Id is int id)
        {
            produto = await db.Produtos.Include(p => p.Receita).FirstOrDefaultAsync(p => p.Id == id, ct)
                      ?? throw new RegraDeNegocioException("Produto não encontrado.");
        }
        else
        {
            produto = new Produto();
            db.Produtos.Add(produto);
        }

        produto.Nome = nome;
        produto.Categoria = categoria;
        produto.Preco = edicao.Preco;

        // Sincroniza a receita no lugar: remove o que saiu, ajusta o que ficou, adiciona o que entrou.
        foreach (var removido in produto.Receita.Where(r => !idsIngredientes.Contains(r.IngredienteId)).ToList())
        {
            produto.Receita.Remove(removido);
            db.ItensReceita.Remove(removido);
        }

        foreach (var item in edicao.Receita)
        {
            var existente = produto.Receita.FirstOrDefault(r => r.IngredienteId == item.IngredienteId);
            if (existente is null)
                produto.Receita.Add(new ItemReceita { Ingrediente = ingredientes[item.IngredienteId], Quantidade = item.Quantidade });
            else
                existente.Quantidade = item.Quantidade;
        }

        await db.SaveChangesAsync(ct);

        var salvo = await db.Produtos
            .AsNoTracking()
            .Include(p => p.Receita).ThenInclude(r => r.Ingrediente)
            .FirstAsync(p => p.Id == produto.Id, ct);

        return ParaDto(salvo);
    }

    public Task DesativarAsync(int produtoId, CancellationToken ct = default) => DefinirAtivoAsync(produtoId, false, ct);

    public Task ReativarAsync(int produtoId, CancellationToken ct = default) => DefinirAtivoAsync(produtoId, true, ct);

    private async Task DefinirAtivoAsync(int produtoId, bool ativo, CancellationToken ct)
    {
        using var trava = await EscritaSerializada.EntrarAsync(ct);
        await using var db = await factory.CreateDbContextAsync(ct);

        var produto = await db.Produtos.FirstOrDefaultAsync(p => p.Id == produtoId, ct)
                      ?? throw new RegraDeNegocioException("Produto não encontrado.");

        produto.Ativo = ativo;
        await db.SaveChangesAsync(ct);
    }

    private static ProdutoMenuDto ParaDto(Produto p)
    {
        var receita = p.Receita
            .OrderBy(r => r.Ingrediente.Nome)
            .Select(r => new ReceitaItemDto(r.IngredienteId, r.Ingrediente.Nome, r.Ingrediente.Unidade, r.Quantidade))
            .ToList();

        // O ingrediente mais escasso limita quantas unidades dá para fazer.
        // Cálculo em memória: o SQLite guarda decimal como texto.
        int? disponivel = null;
        if (p.Receita.Count > 0)
        {
            var maximo = p.Receita.Min(r =>
                r.Quantidade <= 0 ? decimal.MaxValue : Math.Floor(r.Ingrediente.QuantidadeAtual / r.Quantidade));
            disponivel = (int)Math.Min(maximo, int.MaxValue);
        }

        return new ProdutoMenuDto(p.Id, p.Nome, p.Categoria, p.Preco, p.Ativo, receita, disponivel);
    }

    private static (string Nome, string Categoria) Validar(ProdutoEdicao e)
    {
        var nome = (e.Nome ?? string.Empty).Trim();
        var categoria = (e.Categoria ?? string.Empty).Trim();

        if (nome.Length == 0)
            throw new RegraDeNegocioException("Informe o nome do produto.");
        if (nome.Length > 120)
            throw new RegraDeNegocioException("O nome do produto é muito longo (máximo de 120 letras).");
        if (categoria.Length > 60)
            throw new RegraDeNegocioException("A categoria é muito longa (máximo de 60 letras).");
        if (e.Preco < 0)
            throw new RegraDeNegocioException("O preço não pode ser negativo.");

        var receita = e.Receita ?? [];
        if (receita.Any(r => r.Quantidade <= 0))
            throw new RegraDeNegocioException("A quantidade de cada ingrediente da receita deve ser maior que zero.");
        if (receita.GroupBy(r => r.IngredienteId).Any(g => g.Count() > 1))
            throw new RegraDeNegocioException("Há um ingrediente repetido na receita.");

        return (nome, categoria);
    }
}
