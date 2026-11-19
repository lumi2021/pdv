using Microsoft.EntityFrameworkCore;
using Pdv.Core.Entities;
using Pdv.Core.Services;

namespace Pdv.Data.Services;

public sealed class PedidoService(IDbContextFactory<PdvDbContext> factory, PedidoEventos? eventos = null) : IPedidoService
{
    private static readonly Dictionary<StatusPedido, StatusPedido> ProximoStatus = new()
    {
        [StatusPedido.APreparar] = StatusPedido.EmPreparo,
        [StatusPedido.EmPreparo] = StatusPedido.Pronto,
        [StatusPedido.Pronto] = StatusPedido.Entregue
    };

    private static readonly StatusPedido[] StatusDaFila =
        [StatusPedido.APreparar, StatusPedido.EmPreparo, StatusPedido.Pronto];

    public async Task<Pedido> CriarAsync(NovoPedido novo, CancellationToken ct = default)
    {
        Validar(novo);

        using var trava = await EscritaSerializada.EntrarAsync(ct);
        await using var db = await factory.CreateDbContextAsync(ct);

        var idsProdutos = novo.Itens.Select(i => i.ProdutoId).Distinct().ToList();

        // Com rastreamento ligado, o mesmo ingrediente é uma única instância
        // mesmo quando aparece na receita de vários produtos.
        var produtos = await db.Produtos
            .Include(p => p.Receita).ThenInclude(r => r.Ingrediente)
            .Where(p => idsProdutos.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        foreach (var id in idsProdutos)
        {
            if (!produtos.TryGetValue(id, out var produto) || !produto.Ativo)
                throw new RegraDeNegocioException("Um dos produtos do pedido não está mais disponível no menu.");
        }

        // Total de cada ingrediente necessário para o pedido inteiro.
        var necessario = new Dictionary<int, (Ingrediente Ingrediente, decimal Quantidade)>();
        foreach (var item in novo.Itens)
        {
            foreach (var receita in produtos[item.ProdutoId].Receita)
            {
                var qtd = receita.Quantidade * item.Quantidade;
                necessario[receita.IngredienteId] = necessario.TryGetValue(receita.IngredienteId, out var atual)
                    ? (receita.Ingrediente, atual.Quantidade + qtd)
                    : (receita.Ingrediente, qtd);
            }
        }

        var faltas = necessario.Values
            .Where(n => n.Ingrediente.QuantidadeAtual < n.Quantidade)
            .Select(n => new FaltaDeEstoque(
                n.Ingrediente.Id, n.Ingrediente.Nome, n.Quantidade, n.Ingrediente.QuantidadeAtual, n.Ingrediente.Unidade))
            .ToList();

        if (faltas.Count > 0)
            throw new EstoqueInsuficienteException(faltas);

        var ultimoNumero = await db.Pedidos.MaxAsync(p => (int?)p.Numero, ct) ?? 0;

        var pedido = new Pedido
        {
            Numero = ultimoNumero + 1,
            Identificacao = novo.Identificacao.Trim(),
            Observacao = string.IsNullOrWhiteSpace(novo.Observacao) ? null : novo.Observacao.Trim(),
            Status = StatusPedido.APreparar,
            CriadoEm = DateTime.UtcNow
        };

        foreach (var item in novo.Itens)
        {
            var produto = produtos[item.ProdutoId];
            pedido.Itens.Add(new ItemPedido
            {
                Produto = produto,
                NomeProduto = produto.Nome,
                PrecoUnitario = produto.Preco,
                Quantidade = item.Quantidade,
                Observacao = string.IsNullOrWhiteSpace(item.Observacao) ? null : item.Observacao.Trim()
            });
        }

        foreach (var (ingrediente, quantidade) in necessario.Values)
        {
            ingrediente.QuantidadeAtual -= quantidade;
            db.MovimentosEstoque.Add(new MovimentoEstoque
            {
                Ingrediente = ingrediente,
                Tipo = TipoMovimentoEstoque.BaixaPedido,
                Quantidade = -quantidade,
                Pedido = pedido,
                Observacao = $"Pedido #{pedido.Numero}"
            });
        }

        db.Pedidos.Add(pedido);

        // Um único SaveChanges é uma única transação: pedido, itens, baixas e movimentos juntos.
        await db.SaveChangesAsync(ct);
        Avisar(TipoEventoPedido.Criado, pedido);
        return pedido;
    }

    public async Task<Pedido> AlterarStatusAsync(int pedidoId, StatusPedido novoStatus, CancellationToken ct = default)
    {
        if (novoStatus == StatusPedido.Cancelado)
            throw new InvalidOperationException("Para cancelar, use CancelarAsync.");

        using var trava = await EscritaSerializada.EntrarAsync(ct);
        await using var db = await factory.CreateDbContextAsync(ct);

        var pedido = await CarregarAsync(db, pedidoId, ct);

        if (!ProximoStatus.TryGetValue(pedido.Status, out var esperado) || esperado != novoStatus)
            throw new RegraDeNegocioException($"Não é possível mudar o pedido #{pedido.Numero} de {pedido.Status} para {novoStatus}.");

        pedido.Status = novoStatus;
        pedido.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        Avisar(TipoEventoPedido.StatusAlterado, pedido);
        return pedido;
    }

    public async Task<Pedido> CancelarAsync(int pedidoId, bool devolverEstoque = true, CancellationToken ct = default)
    {
        using var trava = await EscritaSerializada.EntrarAsync(ct);
        await using var db = await factory.CreateDbContextAsync(ct);

        var pedido = await CarregarAsync(db, pedidoId, ct);

        if (pedido.Status == StatusPedido.Cancelado)
            throw new RegraDeNegocioException($"O pedido #{pedido.Numero} já está cancelado.");
        if (pedido.Status == StatusPedido.Entregue)
            throw new RegraDeNegocioException($"O pedido #{pedido.Numero} já foi entregue e não pode ser cancelado.");

        if (devolverEstoque)
        {
            // Devolve exatamente o que foi baixado na criação, mesmo que a receita tenha mudado depois.
            var baixas = await db.MovimentosEstoque
                .Include(m => m.Ingrediente)
                .Where(m => m.PedidoId == pedidoId && m.Tipo == TipoMovimentoEstoque.BaixaPedido)
                .ToListAsync(ct);

            foreach (var baixa in baixas)
            {
                var devolvido = -baixa.Quantidade;
                baixa.Ingrediente.QuantidadeAtual += devolvido;
                db.MovimentosEstoque.Add(new MovimentoEstoque
                {
                    Ingrediente = baixa.Ingrediente,
                    Tipo = TipoMovimentoEstoque.EstornoPedido,
                    Quantidade = devolvido,
                    Pedido = pedido,
                    Observacao = $"Cancelamento do pedido #{pedido.Numero}"
                });
            }
        }

        pedido.Status = StatusPedido.Cancelado;
        pedido.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        Avisar(TipoEventoPedido.Cancelado, pedido);
        return pedido;
    }

    public async Task<IReadOnlyList<Pedido>> ListarFilaAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.Pedidos
            .AsNoTracking()
            .Include(p => p.Itens)
            .Where(p => StatusDaFila.Contains(p.Status))
            .OrderBy(p => p.CriadoEm).ThenBy(p => p.Id)
            .ToListAsync(ct);
    }

    private void Avisar(TipoEventoPedido tipo, Pedido pedido) =>
        eventos?.Publicar(new PedidoEvento(tipo, pedido.Id, pedido.Numero, pedido.Status));

    private static async Task<Pedido> CarregarAsync(PdvDbContext db, int pedidoId, CancellationToken ct) =>
        await db.Pedidos.Include(p => p.Itens).FirstOrDefaultAsync(p => p.Id == pedidoId, ct)
        ?? throw new RegraDeNegocioException("Pedido não encontrado.");

    private static void Validar(NovoPedido novo)
    {
        if (novo.Itens is null || novo.Itens.Count == 0)
            throw new RegraDeNegocioException("O pedido precisa ter pelo menos um item.");
        if (novo.Itens.Any(i => i.Quantidade <= 0))
            throw new RegraDeNegocioException("A quantidade de cada item deve ser maior que zero.");
        if (string.IsNullOrWhiteSpace(novo.Identificacao))
            throw new RegraDeNegocioException("Informe a mesa ou o nome do cliente.");
    }
}
