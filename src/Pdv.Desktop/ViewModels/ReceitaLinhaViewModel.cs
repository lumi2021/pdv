using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pdv.Core.Entities;

namespace Pdv.Desktop.ViewModels;

/// <summary>Uma linha editável da receita: ingrediente + quantidade usada por unidade vendida.</summary>
public partial class ReceitaLinhaViewModel(
    IReadOnlyList<IngredienteOpcao> opcoes,
    Action<ReceitaLinhaViewModel> remover) : ViewModelBase
{
    public IReadOnlyList<IngredienteOpcao> Opcoes { get; } = opcoes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UnidadeTexto))]
    private IngredienteOpcao? _ingrediente;

    [ObservableProperty]
    private decimal? _quantidade;

    public string UnidadeTexto => Ingrediente?.Unidade.Abreviacao() ?? string.Empty;

    [RelayCommand]
    private void Remover() => remover(this);
}
