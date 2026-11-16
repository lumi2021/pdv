using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pdv.Core.Entities;
using Pdv.Core.Services;

namespace Pdv.Desktop.ViewModels;

public sealed record OpcaoUnidade(UnidadeMedida Valor, string Nome);

public partial class EstoqueViewModel : SecaoViewModel, IAtualizavel
{
    private readonly IEstoqueService _estoque;

    public EstoqueViewModel(IEstoqueService estoque)
        : base("Estoque", "📦", "Ingredientes, entradas e alertas")
    {
        _estoque = estoque;
        _unidadeSelecionada = Unidades[0];
    }

    public IReadOnlyList<OpcaoUnidade> Unidades { get; } =
    [
        new(UnidadeMedida.Unidade, "Unidade (un)"),
        new(UnidadeMedida.Grama, "Grama (g)"),
        new(UnidadeMedida.Mililitro, "Mililitro (ml)")
    ];

    public ObservableCollection<IngredienteItemViewModel> Ingredientes { get; } = [];

    // ---- Seleção e modo do formulário -------------------------------------------------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormularioVisivel), nameof(EmEdicao), nameof(PodeMovimentar),
        nameof(PodeDesativar), nameof(PodeReativar), nameof(TituloFormulario), nameof(TextoBotaoSalvar))]
    private IngredienteItemViewModel? _selecionado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormularioVisivel), nameof(EmEdicao), nameof(PodeMovimentar),
        nameof(PodeDesativar), nameof(PodeReativar), nameof(TituloFormulario), nameof(TextoBotaoSalvar))]
    private bool _emCriacao;

    public bool FormularioVisivel => EmCriacao || Selecionado is not null;
    public bool EmEdicao => !EmCriacao && Selecionado is not null;
    public bool PodeMovimentar => EmEdicao && Selecionado!.Ativo;
    public bool PodeDesativar => PodeMovimentar;
    public bool PodeReativar => EmEdicao && !Selecionado!.Ativo;
    public string TituloFormulario => EmCriacao ? "Novo ingrediente" : "Editar ingrediente";
    public string TextoBotaoSalvar => EmCriacao ? "Criar ingrediente" : "Salvar alterações";

    // ---- Campos do formulário ---------------------------------------------------------------

    [ObservableProperty] private string _nome = string.Empty;
    [ObservableProperty] private OpcaoUnidade _unidadeSelecionada;
    [ObservableProperty] private decimal? _estoqueMinimo = 0;
    [ObservableProperty] private decimal? _saldoInicial = 0;
    [ObservableProperty] private decimal? _quantidadeEntrada;
    [ObservableProperty] private string _observacaoEntrada = string.Empty;
    [ObservableProperty] private decimal? _saldoContado;

    // ---- Mensagens --------------------------------------------------------------------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemErro))]
    private string _erro = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemSucesso))]
    private string _sucesso = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemAlerta))]
    private string _alertaEstoqueBaixo = string.Empty;

    [ObservableProperty]
    private bool _mostrarDesativados;

    public bool TemErro => Erro.Length > 0;
    public bool TemSucesso => Sucesso.Length > 0;
    public bool TemAlerta => AlertaEstoqueBaixo.Length > 0;

    // ---- Carregamento -----------------------------------------------------------------------

    public Task AtualizarAsync() => RecarregarAsync(Selecionado?.Id);

    partial void OnMostrarDesativadosChanged(bool value) => _ = AtualizarAsync();

    partial void OnSelecionadoChanged(IngredienteItemViewModel? value)
    {
        LimparMensagens();
        if (value is null) return;

        EmCriacao = false;
        Nome = value.Nome;
        UnidadeSelecionada = Unidades.First(u => u.Valor == value.Unidade);
        EstoqueMinimo = value.Minimo;
        SaldoContado = value.Saldo;
        QuantidadeEntrada = null;
        ObservacaoEntrada = string.Empty;
    }

    private async Task RecarregarAsync(int? selecionarId)
    {
        try
        {
            var todos = await _estoque.ListarAsync(MostrarDesativados);
            var baixos = await _estoque.ListarEstoqueBaixoAsync();

            Ingredientes.Clear();
            foreach (var ingrediente in todos)
                Ingredientes.Add(new IngredienteItemViewModel(ingrediente));

            Selecionado = selecionarId is int id
                ? Ingredientes.FirstOrDefault(i => i.Id == id)
                : null;

            AlertaEstoqueBaixo = baixos.Count switch
            {
                0 => string.Empty,
                1 => $"1 ingrediente com estoque baixo: {baixos[0].Nome}",
                _ => $"{baixos.Count} ingredientes com estoque baixo: {string.Join(", ", baixos.Select(b => b.Nome))}"
            };
        }
        catch (Exception)
        {
            Erro = "Não foi possível carregar o estoque. Abra a tela de novo.";
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
        UnidadeSelecionada = Unidades[0];
        EstoqueMinimo = 0;
        SaldoInicial = 0;
    }

    [RelayCommand]
    private void Cancelar()
    {
        LimparMensagens();
        EmCriacao = false;
        Selecionado = null;
    }

    [RelayCommand]
    private Task SalvarAsync() => ExecutarAsync(async () =>
    {
        if (EmCriacao)
        {
            var criado = await _estoque.CriarIngredienteAsync(
                Nome, UnidadeSelecionada.Valor, EstoqueMinimo ?? 0, SaldoInicial ?? 0);

            EmCriacao = false;
            await RecarregarAsync(criado.Id);
            return $"\"{criado.Nome}\" foi criado.";
        }

        var atual = Selecionado!;
        var atualizado = await _estoque.AtualizarIngredienteAsync(atual.Id, Nome, EstoqueMinimo ?? 0);
        await RecarregarAsync(atualizado.Id);
        return "Alterações salvas.";
    });

    [RelayCommand]
    private Task RegistrarEntradaAsync() => ExecutarAsync(async () =>
    {
        var atual = Selecionado!;
        if (QuantidadeEntrada is not > 0)
            throw new RegraDeNegocioException("Informe a quantidade que entrou.");

        var observacao = string.IsNullOrWhiteSpace(ObservacaoEntrada) ? null : ObservacaoEntrada.Trim();
        await _estoque.RegistrarEntradaAsync(atual.Id, QuantidadeEntrada.Value, observacao);
        await RecarregarAsync(atual.Id);
        return "Entrada registrada.";
    });

    [RelayCommand]
    private Task AjustarSaldoAsync() => ExecutarAsync(async () =>
    {
        var atual = Selecionado!;
        if (SaldoContado is null)
            throw new RegraDeNegocioException("Informe a quantidade contada.");

        await _estoque.AjustarSaldoAsync(atual.Id, SaldoContado.Value, "Contagem física");
        await RecarregarAsync(atual.Id);
        return "Saldo ajustado.";
    });

    [RelayCommand]
    private Task DesativarAsync() => ExecutarAsync(async () =>
    {
        var atual = Selecionado!;
        await _estoque.DesativarAsync(atual.Id);
        await RecarregarAsync(selecionarId: MostrarDesativados ? atual.Id : null);
        return $"\"{atual.Nome}\" foi desativado.";
    });

    [RelayCommand]
    private Task ReativarAsync() => ExecutarAsync(async () =>
    {
        var atual = Selecionado!;
        await _estoque.ReativarAsync(atual.Id);
        await RecarregarAsync(atual.Id);
        return $"\"{atual.Nome}\" foi reativado.";
    });

    // ---- Auxiliares -------------------------------------------------------------------------

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
