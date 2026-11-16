using Pdv.Core.Entities;

namespace Pdv.Core.Services;

/// <summary>
/// Erro esperado de regra de negócio. A mensagem já está em linguagem simples
/// e pode ser mostrada direto ao usuário (no app ou no celular do garçom).
/// </summary>
public class RegraDeNegocioException(string message) : Exception(message);

public sealed record FaltaDeEstoque(
    int IngredienteId,
    string Ingrediente,
    decimal Necessario,
    decimal Disponivel,
    UnidadeMedida Unidade)
{
    public decimal Faltam => Necessario - Disponivel;
}

public sealed class EstoqueInsuficienteException(IReadOnlyList<FaltaDeEstoque> faltas)
    : RegraDeNegocioException(Montar(faltas))
{
    public IReadOnlyList<FaltaDeEstoque> Faltas { get; } = faltas;

    private static string Montar(IReadOnlyList<FaltaDeEstoque> faltas) =>
        "Estoque insuficiente: " + string.Join("; ",
            faltas.Select(f => $"{f.Ingrediente} (faltam {f.Faltam:0.##} {f.Unidade.Abreviacao()})"));
}
