using System.Text.Json;
using Pdv.Core.Entities;
using Pdv.Core.Services;

namespace Pdv.Data.Services;

/// <summary>Monta e lê a nota: a cópia do pedido vai em JSON dentro da própria nota.</summary>
internal static class NotaBuilder
{
    private static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web);

    public static NotaInterna Criar(Pedido pedido, int numero)
    {
        var conteudo = new NotaConteudo(
            pedido.Numero,
            pedido.Identificacao,
            pedido.Observacao,
            DateTime.SpecifyKind(pedido.CriadoEm, DateTimeKind.Utc),
            pedido.Itens
                .Select(i => new NotaItem(i.NomeProduto, i.PrecoUnitario, i.Quantidade, i.Observacao))
                .ToList());

        return new NotaInterna
        {
            Pedido = pedido,
            Numero = numero,
            EmitidaEm = DateTime.UtcNow,
            Total = conteudo.Itens.Sum(i => i.Subtotal),
            ConteudoJson = JsonSerializer.Serialize(conteudo, Opcoes)
        };
    }

    public static NotaDetalhe Ler(NotaInterna nota)
    {
        var conteudo = JsonSerializer.Deserialize<NotaConteudo>(nota.ConteudoJson, Opcoes)
                       ?? throw new InvalidOperationException($"O conteúdo da nota {nota.Numero} está ilegível.");

        return new NotaDetalhe(
            nota.Id, nota.Numero, DateTime.SpecifyKind(nota.EmitidaEm, DateTimeKind.Utc), nota.Total, conteudo);
    }
}
