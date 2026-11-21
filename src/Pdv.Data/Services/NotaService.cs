using Microsoft.EntityFrameworkCore;
using Pdv.Core.Entities;
using Pdv.Core.Services;

namespace Pdv.Data.Services;

public sealed class NotaService(IDbContextFactory<PdvDbContext> factory) : INotaService
{
    public async Task<IReadOnlyList<NotaResumo>> ListarAsync(
        string? busca = null, DateTime? desdeUtc = null, int limite = 200, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var consulta = db.NotasInternas.AsNoTracking().Include(n => n.Pedido).AsQueryable();

        if (desdeUtc is { } desde)
            consulta = consulta.Where(n => n.EmitidaEm >= desde);

        var termo = busca?.Trim().TrimStart('#');
        if (!string.IsNullOrEmpty(termo))
        {
            // LIKE no SQLite ignora maiúsculas/minúsculas; % e _ digitados valem como texto comum.
            var padrao = "%" + termo.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";

            if (int.TryParse(termo, out var numero))
                consulta = consulta.Where(n => n.Numero == numero || n.Pedido.Numero == numero
                                               || EF.Functions.Like(n.Pedido.Identificacao, padrao, "\\"));
            else
                consulta = consulta.Where(n => EF.Functions.Like(n.Pedido.Identificacao, padrao, "\\"));
        }

        var notas = await consulta.OrderByDescending(n => n.Numero).Take(Math.Max(1, limite)).ToListAsync(ct);

        return notas
            .Select(n => new NotaResumo(
                n.Id, n.Numero, n.Pedido.Numero, n.Pedido.Identificacao,
                DateTime.SpecifyKind(n.EmitidaEm, DateTimeKind.Utc), n.Total))
            .ToList();
    }

    public async Task<NotaDetalhe?> ObterAsync(int notaId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var nota = await db.NotasInternas.AsNoTracking().FirstOrDefaultAsync(n => n.Id == notaId, ct);
        return nota is null ? null : NotaBuilder.Ler(nota);
    }

    public async Task<NotaDetalhe?> ObterPorPedidoAsync(int pedidoId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var nota = await db.NotasInternas.AsNoTracking().FirstOrDefaultAsync(n => n.PedidoId == pedidoId, ct);
        return nota is null ? null : NotaBuilder.Ler(nota);
    }

    public async Task<NotaDetalhe> EmitirAsync(int pedidoId, CancellationToken ct = default)
    {
        using var trava = await EscritaSerializada.EntrarAsync(ct);
        await using var db = await factory.CreateDbContextAsync(ct);

        var existente = await db.NotasInternas.FirstOrDefaultAsync(n => n.PedidoId == pedidoId, ct);
        if (existente is not null)
            return NotaBuilder.Ler(existente);

        var pedido = await db.Pedidos.Include(p => p.Itens).FirstOrDefaultAsync(p => p.Id == pedidoId, ct)
                     ?? throw new RegraDeNegocioException("Pedido não encontrado.");

        if (pedido.Status != StatusPedido.Entregue)
            throw new RegraDeNegocioException($"A nota do pedido #{pedido.Numero} só é emitida depois que ele for entregue.");

        var nota = NotaBuilder.Criar(pedido, await ProximoNumeroAsync(db, ct));
        db.NotasInternas.Add(nota);
        await db.SaveChangesAsync(ct);
        return NotaBuilder.Ler(nota);
    }

    public async Task<int> EmitirPendentesAsync(CancellationToken ct = default)
    {
        using var trava = await EscritaSerializada.EntrarAsync(ct);
        await using var db = await factory.CreateDbContextAsync(ct);

        var semNota = await db.Pedidos
            .Include(p => p.Itens)
            .Where(p => p.Status == StatusPedido.Entregue && !db.NotasInternas.Any(n => n.PedidoId == p.Id))
            .OrderBy(p => p.Numero)
            .ToListAsync(ct);

        if (semNota.Count == 0) return 0;

        var numero = await ProximoNumeroAsync(db, ct);
        foreach (var pedido in semNota)
            db.NotasInternas.Add(NotaBuilder.Criar(pedido, numero++));

        await db.SaveChangesAsync(ct);
        return semNota.Count;
    }

    /// <summary>Numeração das notas é própria, sequencial e nunca reaproveita. Chamar com a trava de escrita.</summary>
    internal static async Task<int> ProximoNumeroAsync(PdvDbContext db, CancellationToken ct) =>
        (await db.NotasInternas.MaxAsync(n => (int?)n.Numero, ct) ?? 0) + 1;
}
