using Pdv.Core.Entities;

namespace Pdv.Desktop.ViewModels;

/// <summary>Ingrediente como opção de escolha na receita. Records comparam por valor, então a seleção do ComboBox casa.</summary>
public sealed record IngredienteOpcao(int Id, string Nome, UnidadeMedida Unidade)
{
    public string Texto => $"{Nome} ({Unidade.Abreviacao()})";
}
