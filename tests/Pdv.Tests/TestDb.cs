using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pdv.Core.Entities;
using Pdv.Data;

namespace Pdv.Tests;

/// <summary>SQLite em memória (uma conexão aberta mantém o banco vivo) usado como IDbContextFactory.</summary>
public sealed class TestDb : IDbContextFactory<PdvDbContext>, IDisposable
{
    public sealed record Ids(int Pao, int Carne, int XBurger, int Descontinuado);

    private readonly SqliteConnection _conexao;
    private readonly DbContextOptions<PdvDbContext> _opcoes;

    public TestDb()
    {
        _conexao = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        _conexao.Open();
        _opcoes = new DbContextOptionsBuilder<PdvDbContext>().UseSqlite(_conexao).Options;

        using var db = CreateDbContext();
        db.Database.EnsureCreated();
    }

    public PdvDbContext CreateDbContext() => new(_opcoes);

    /// <summary>Pão (un, mín. 2) e Carne (g, mín. 200). X-Burger = 1 pão + 150 g de carne, R$ 20.</summary>
    public Ids Seed(decimal pao = 10, decimal carne = 1000)
    {
        using var db = CreateDbContext();

        var paoEnt = new Ingrediente { Nome = "Pão", Unidade = UnidadeMedida.Unidade, QuantidadeAtual = pao, EstoqueMinimo = 2 };
        var carneEnt = new Ingrediente { Nome = "Carne", Unidade = UnidadeMedida.Grama, QuantidadeAtual = carne, EstoqueMinimo = 200 };

        var xBurger = new Produto
        {
            Nome = "X-Burger",
            Categoria = "Lanches",
            Preco = 20m,
            Receita =
            [
                new ItemReceita { Ingrediente = paoEnt, Quantidade = 1 },
                new ItemReceita { Ingrediente = carneEnt, Quantidade = 150 }
            ]
        };

        var descontinuado = new Produto { Nome = "Prato descontinuado", Preco = 10m, Ativo = false };

        db.AddRange(paoEnt, carneEnt, xBurger, descontinuado);
        db.SaveChanges();

        return new Ids(paoEnt.Id, carneEnt.Id, xBurger.Id, descontinuado.Id);
    }

    public decimal Saldo(int ingredienteId)
    {
        using var db = CreateDbContext();
        return db.Ingredientes.AsNoTracking().Single(i => i.Id == ingredienteId).QuantidadeAtual;
    }

    public List<MovimentoEstoque> Movimentos()
    {
        using var db = CreateDbContext();
        return db.MovimentosEstoque.AsNoTracking().OrderBy(m => m.Id).ToList();
    }

    public void Dispose() => _conexao.Dispose();
}
