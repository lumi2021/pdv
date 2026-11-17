using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pdv.Core.Services;

namespace Pdv.Desktop.ViewModels;

public partial class CardapioViewModel : SecaoViewModel, IAtualizavel
{
    private readonly IMenuService _menu;
    private readonly IEstoqueService _estoque;
    private IReadOnlyList<IngredienteOpcao> _opcoes = [];

    public CardapioViewModel(IMenuService menu, IEstoqueService estoque)
        : base("Cardápio", "🍔", "Produtos, preços e receitas")
    {
        _menu = menu;
        _estoque = estoque;
    }

    public ObservableCollection<ProdutoItemViewModel> Produtos { get; } = [];
    public ObservableCollection<ReceitaLinhaViewModel> Receita { get; } = [];

    // ---- Seleção e modo do formulário -------------------------------------------------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormularioVisivel), nameof(EmEdicao), nameof(PodeDesativar),
        nameof(PodeReativar), nameof(TituloFormulario), nameof(TextoBotaoSalvar))]
    private ProdutoItemViewModel? _selecionado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormularioVisivel), nameof(EmEdicao), nameof(PodeDesativar),
        nameof(PodeReativar), nameof(TituloFormulario), nameof(TextoBotaoSalvar))]
    private bool _emCriacao;

    public bool FormularioVisivel => EmCriacao || Selecionado is not null;
    public bool EmEdicao => !EmCriacao && Selecionado is not null;
    public bool PodeDesativar => EmEdicao && Selecionado!.Ativo;
    public bool PodeReativar => EmEdicao && !Selecionado!.Ativo;
    public string TituloFormulario => EmCriacao ? "Novo produto" : "Editar produto";
    public string TextoBotaoSalvar => EmCriacao ? "Criar produto" : "Salvar alterações";

    // ---- Campos do formulário ---------------------------------------------------------------

    [ObservableProperty] private string _nome = string.Empty;
    [ObservableProperty] private string _categoria = string.Empty;
    [ObservableProperty] private decimal? _preco = 0;

    // ---- Mensagens e filtros ----------------------------------------------------------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemErro))]
    private string _erro = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemSucesso))]
    private string _sucesso = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemAlerta))]
    private string _alertaEsgotados = string.Empty;

    [ObservableProperty]
    private bool _mostrarDesativados;

    public bool TemErro => Erro.Length > 0;
    public bool TemSucesso => Sucesso.Length > 0;
    public bool TemAlerta => AlertaEsgotados.Length > 0;

    // ---- Carregamento -----------------------------------------------------------------------

    public Task AtualizarAsync() => RecarregarAsync(Selecionado?.Id);

    partial void OnMostrarDesativadosChanged(bool value) => _ = AtualizarAsync();

    partial void OnSelecionadoChanged(ProdutoItemViewModel? value)
    {
        LimparMensagens();
        if (value is null) return;

        EmCriacao = false;
        Nome = value.Nome;
        Categoria = value.Categoria;
        Preco = value.Preco;

        Receita.Clear();
        foreach (var item in value.Receita)
        {
            var opcao = _opcoes.FirstOrDefault(o => o.Id == item.IngredienteId)
                        ?? new IngredienteOpcao(item.IngredienteId, item.Ingrediente, item.Unidade);
            Receita.Add(NovaLinha(opcao, item.Quantidade));
        }
    }

    private async Task RecarregarAsync(int? selecionarId)
    {
        try
        {
            var ingredientes = await _estoque.ListarAsync();
            _opcoes = ingredientes.Select(i => new IngredienteOpcao(i.Id, i.Nome, i.Unidade)).ToList();

            var produtos = await _menu.ListarAsync(MostrarDesativados);

            Produtos.Clear();
            foreach (var produto in produtos)
                Produtos.Add(new ProdutoItemViewModel(produto));

            Selecionado = selecionarId is int id
                ? Produtos.FirstOrDefault(p => p.Id == id)
                : null;

            var esgotados = Produtos.Where(p => p.Esgotado).Select(p => p.Nome).ToList();
            AlertaEsgotados = esgotados.Count switch
            {
                0 => string.Empty,
                1 => $"1 produto esgotado: {esgotados[0]}",
                _ => $"{esgotados.Count} produtos esgotados: {string.Join(", ", esgotados)}"
            };
        }
        catch (Exception)
        {
            Erro = "Não foi possível carregar o cardápio. Abra a tela de novo.";
        }
    }

    // ---- Comandos ---------------------------------------------------------------------------

    [RelayCommand]
    private void Novo()
    {
        LimparMensagens();
        Selecionado = null;
        EmCriacao = true;
        Nome = string.Empty;
        Categoria = string.Empty;
        Preco = 0;
        Receita.Clear();
    }

    [RelayCommand]
    private void Cancelar()
    {
        LimparMensagens();
        EmCriacao = false;
        Selecionado = null;
    }

    [RelayCommand]
    private void AdicionarIngrediente()
    {
        if (_opcoes.Count == 0)
        {
            Erro = "Cadastre os ingredientes na tela Estoque antes de montar a receita.";
            return;
        }

        Erro = string.Empty;
        Receita.Add(NovaLinha(null, null));
    }

    [RelayCommand]
    private Task SalvarAsync() => ExecutarAsync(async () =>
    {
        if (Preco is null)
            throw new RegraDeNegocioException("Informe o preço do produto.");
        if (Receita.Any(l => l.Ingrediente is null))
            throw new RegraDeNegocioException("Escolha o ingrediente em todas as linhas da receita (ou remova a linha).");
        if (Receita.Any(l => l.Quantidade is not > 0))
            throw new RegraDeNegocioException("Informe a quantidade de cada ingrediente da receita.");

        var receita = Receita
            .Select(l => new ReceitaItemEdicao(l.Ingrediente!.Id, l.Quantidade!.Value))
            .ToList();

        var edicao = new ProdutoEdicao(EmCriacao ? null : Selecionado!.Id, Nome, Categoria, Preco.Value, receita);
        var criando = EmCriacao;

        var salvo = await _menu.SalvarAsync(edicao);

        EmCriacao = false;
        await RecarregarAsync(salvo.Id);
        return criando ? $"\"{salvo.Nome}\" foi criado." : "Alterações salvas.";
    });

    [RelayCommand]
    private Task DesativarAsync() => ExecutarAsync(async () =>
    {
        var atual = Selecionado!;
        await _menu.DesativarAsync(atual.Id);
        await RecarregarAsync(MostrarDesativados ? atual.Id : null);
        return $"\"{atual.Nome}\" saiu do cardápio.";
    });

    [RelayCommand]
    private Task ReativarAsync() => ExecutarAsync(async () =>
    {
        var atual = Selecionado!;
        await _menu.ReativarAsync(atual.Id);
        await RecarregarAsync(atual.Id);
        return $"\"{atual.Nome}\" voltou ao cardápio.";
    });

    // ---- Auxiliares -------------------------------------------------------------------------

    private ReceitaLinhaViewModel NovaLinha(IngredienteOpcao? ingrediente, decimal? quantidade)
    {
        // Se o ingrediente da receita já não está entre os ativos (produto antigo), mantém-no como opção.
        var opcoes = ingrediente is not null && !_opcoes.Contains(ingrediente)
            ? _opcoes.Append(ingrediente).ToList()
            : _opcoes;

        return new ReceitaLinhaViewModel(opcoes, linha => Receita.Remove(linha))
        {
            Ingrediente = ingrediente,
            Quantidade = quantidade
        };
    }

    /// <summary>Roda uma ação e mostra o texto devolvido como sucesso, ou o motivo da recusa como erro.</summary>
    private async Task ExecutarAsync(Func<Task<string>> acao)
    {
        LimparMensagens();
        try
        {
            Sucesso = await acao();
        }
        catch (RegraDeNegocioException ex)
        {
            Erro = ex.Message;
        }
        catch (Exception)
        {
            Erro = "Não foi possível concluir. Tente de novo.";
        }
    }

    private void LimparMensagens()
    {
        Erro = string.Empty;
        Sucesso = string.Empty;
    }
}
