using Pdv.Core.Services;

namespace Pdv.Desktop.ViewModels;

/// <summary>Linha da lista do cardápio: uma foto imutável do produto no momento do carregamento.</summary>
public sealed class ProdutoItemViewModel(ProdutoMenuDto produto)
{
    public int Id { get; } = produto.Id;
    public string Nome { get; } = produto.Nome;
    public string Categoria { get; } = produto.Categoria;
    public decimal Preco { get; } = produto.Preco;
    public bool Ativo { get; } = produto.Ativo;
    public int? Disponivel { get; } = produto.Disponivel;
    public IReadOnlyList<ReceitaItemDto> Receita { get; } = produto.Receita;

    public string CategoriaTexto => string.IsNullOrWhiteSpace(Categoria) ? "Sem categoria" : Categoria;
    public string PrecoTexto => $"R$ {Preco:0.00}";

    /// <summary>Texto neutro de disponibilidade (vazio quando há um problema a destacar em <see cref="Situacao"/>).</summary>
    public string DisponibilidadeTexto =>
        !Ativo ? string.Empty :
        Disponivel is null ? "Sem controle de estoque" :
        Disponivel == 0 ? string.Empty :
        Disponivel == 1 ? "1 disponível" :
        $"{Disponivel} disponíveis";

    /// <summary>Problema a destacar em vermelho: desativado ou esgotado.</summary>
    public string Situacao =>
        !Ativo ? "Desativado" :
        Disponivel == 0 ? "Esgotado" :
        string.Empty;

    public bool Esgotado => Ativo && Disponivel == 0;
}
