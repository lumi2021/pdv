using Pdv.Core.Entities;

namespace Pdv.Core.Services;

public sealed record ReceitaItemDto(int IngredienteId, string Ingrediente, UnidadeMedida Unidade, decimal Quantidade);

/// <summary>
/// Produto do cardápio como a interface (app e celular) precisa ver.
/// <see cref="Disponivel"/>: quantas unidades ainda dá para vender com o estoque atual;
/// <c>null</c> quando o produto não tem receita (sem controle de estoque).
/// </summary>
public sealed record ProdutoMenuDto(
    int Id,
    string Nome,
    string Categoria,
    decimal Preco,
    bool Ativo,
    IReadOnlyList<ReceitaItemDto> Receita,
    int? Disponivel);

public sealed record ReceitaItemEdicao(int IngredienteId, decimal Quantidade);

/// <summary>Dados para criar (<see cref="Id"/> nulo) ou alterar um produto e sua receita.</summary>
public sealed record ProdutoEdicao(
    int? Id,
    string Nome,
    string Categoria,
    decimal Preco,
    IReadOnlyList<ReceitaItemEdicao> Receita);
