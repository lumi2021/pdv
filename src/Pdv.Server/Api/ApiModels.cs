using Pdv.Core.Entities;

namespace Pdv.Server.Api;

// Contrato JSON entre o servidor e o PDV do celular. Propriedades em camelCase, enums como texto.

public sealed record StatusApi(string Nome, string Versao, DateTime Agora);

public sealed record ParearApi(string? Codigo, string? Nome);

/// <summary>O token só é entregue uma vez, no pareamento; o celular o guarda e o envia em cada requisição.</summary>
public sealed record ParearRespostaApi(string Token, string Nome);

public sealed record EuApi(string Nome);

/// <summary>Produto como o garçom vê. Não expõe receita nem saldos de estoque.</summary>
public sealed record ProdutoCardapioApi(int Id, string Nome, string Categoria, decimal Preco, int? Disponivel);

public sealed record ItemPedidoApi(
    int ProdutoId, string Nome, decimal PrecoUnitario, int Quantidade, string? Observacao, decimal Subtotal);

public sealed record PedidoApi(
    int Id,
    int Numero,
    string Identificacao,
    string? Observacao,
    StatusPedido Status,
    decimal Total,
    DateTime CriadoEm,
    IReadOnlyList<ItemPedidoApi> Itens);

public sealed record NovoItemPedidoApi(int ProdutoId, int Quantidade, string? Observacao);

public sealed record NovoPedidoApi(string Identificacao, string? Observacao, List<NovoItemPedidoApi>? Itens);

public sealed record AlterarStatusApi(StatusPedido Status);

public sealed record CancelarPedidoApi(bool DevolverEstoque = true);

public sealed record FaltaApi(string Ingrediente, decimal Necessario, decimal Disponivel, string Unidade);

/// <summary>Toda resposta de erro tem uma mensagem pronta para mostrar ao usuário.</summary>
public sealed record ErroApi(string Mensagem, IReadOnlyList<FaltaApi>? Faltas = null);
