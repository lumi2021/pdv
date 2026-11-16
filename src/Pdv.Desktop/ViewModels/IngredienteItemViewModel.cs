using Pdv.Core.Entities;

namespace Pdv.Desktop.ViewModels;

/// <summary>Linha da lista de estoque: uma foto imutável do ingrediente no momento do carregamento.</summary>
public sealed class IngredienteItemViewModel(Ingrediente ingrediente)
{
    public int Id { get; } = ingrediente.Id;
    public string Nome { get; } = ingrediente.Nome;
    public UnidadeMedida Unidade { get; } = ingrediente.Unidade;
    public decimal Saldo { get; } = ingrediente.QuantidadeAtual;
    public decimal Minimo { get; } = ingrediente.EstoqueMinimo;
    public bool Ativo { get; } = ingrediente.Ativo;

    public string SaldoTexto => $"{Saldo:0.##} {Unidade.Abreviacao()}";
    public string MinimoTexto => $"mínimo: {Minimo:0.##} {Unidade.Abreviacao()}";

    public string Situacao =>
        !Ativo ? "Desativado" :
        Saldo <= Minimo ? "Estoque baixo" :
        string.Empty;
}
