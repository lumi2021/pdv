using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pdv.Core.Infra;
using Pdv.Core.Services;
using Pdv.Data;

namespace Pdv.Desktop.ViewModels;

/// <summary>Assistente da primeira abertura: nome do negócio, cardápio de exemplo opcional e conexão do celular.</summary>
public partial class AssistenteViewModel : ViewModelBase
{
    private const int Boasvindas = 0, Nome = 1, Exemplo = 2, Celular_ = 3, Pronto_ = 4;

    private readonly IConfiguracaoService _configuracao;
    private readonly IEstoqueService _estoque;
    private readonly IMenuService _menu;
    private readonly Func<Task> _aoConcluir;
    private bool _exemploCriado;

    public AssistenteViewModel(
        IConfiguracaoService configuracao, IEstoqueService estoque, IMenuService menu,
        CelularViewModel celular, Func<Task> aoConcluir)
    {
        _configuracao = configuracao;
        _estoque = estoque;
        _menu = menu;
        _aoConcluir = aoConcluir;
        Celular = celular;
        _nomeDoNegocio = configuracao.Atual.NomeDoNegocio;
    }

    /// <summary>Mesma tela da seção Celular, reaproveitada no passo de conexão.</summary>
    public CelularViewModel Celular { get; }

    public string PastaDeBackups => DatabasePaths.BackupFolder();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EhBoasVindas), nameof(EhNome), nameof(EhExemplo), nameof(EhCelular),
        nameof(EhPronto), nameof(PodeVoltar), nameof(TextoAvancar), nameof(Titulo), nameof(Contagem))]
    private int _passo = Boasvindas;

    [ObservableProperty] private string _nomeDoNegocio;
    [ObservableProperty] private bool _criarExemplo = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemErro))]
    private string _erro = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeAvancar))]
    private bool _ocupado;

    /// <summary>O passo do cardápio de exemplo só aparece num sistema ainda vazio.</summary>
    public bool OferecerExemplo { get; private set; }

    public bool EhBoasVindas => Passo == Boasvindas;
    public bool EhNome => Passo == Nome;
    public bool EhExemplo => Passo == Exemplo;
    public bool EhCelular => Passo == Celular_;
    public bool EhPronto => Passo == Pronto_;
    public bool PodeVoltar => Passo > Boasvindas && Passo < Pronto_ && !Ocupado;
    public bool PodeAvancar => !Ocupado;
    public bool TemErro => Erro.Length > 0;

    public string TextoAvancar => Passo switch
    {
        Boasvindas => "Começar",
        Pronto_ => "Abrir o PDV",
        Celular_ => "Continuar",
        _ => "Continuar"
    };

    public string Titulo => Passo switch
    {
        Boasvindas => "Boas-vindas ao PDV Lanchonete",
        Nome => "Como se chama o seu negócio?",
        Exemplo => "Começar com um cardápio de exemplo?",
        Celular_ => "Conecte o celular do garçom",
        _ => "Tudo pronto!"
    };

    public string Contagem
    {
        get
        {
            if (Passo is Boasvindas or Pronto_) return string.Empty;
            var total = OferecerExemplo ? 3 : 2;
            var atual = Passo == Celular_ && !OferecerExemplo ? 2 : Passo;
            return $"Passo {atual} de {total}";
        }
    }

    /// <summary>Descobre se o sistema está vazio (para oferecer o exemplo). Nunca lança.</summary>
    public async Task PrepararAsync()
    {
        try
        {
            var semIngredientes = (await _estoque.ListarAsync(incluirDesativados: true)).Count == 0;
            var semProdutos = (await _menu.ListarAsync(incluirDesativados: true)).Count == 0;
            OferecerExemplo = semIngredientes && semProdutos;
        }
        catch
        {
            OferecerExemplo = false;
        }
    }

    [RelayCommand]
    private void Voltar()
    {
        Erro = string.Empty;
        Passo = Passo == Celular_ && !OferecerExemplo ? Nome : Passo - 1;
    }

    [RelayCommand]
    private async Task AvancarAsync()
    {
        Erro = string.Empty;
        Ocupado = true;
        try
        {
            switch (Passo)
            {
                case Nome:
                    Passo = OferecerExemplo ? Exemplo : Celular_;
                    break;

                case Exemplo:
                    if (CriarExemplo && !_exemploCriado)
                    {
                        await DadosDeExemplo.CriarAsync(_estoque, _menu);
                        _exemploCriado = true;
                    }
                    Passo = Celular_;
                    break;

                case Pronto_:
                    await _configuracao.SalvarAsync(new ConfiguracaoApp(NomeDoNegocio, PrimeiroUsoConcluido: true));
                    await _aoConcluir();
                    return;

                default:
                    Passo++;
                    break;
            }

            // Na tela do celular, os endereços e o QR Code são lidos na hora de mostrar.
            if (Passo == Celular_)
                await Celular.AtualizarAsync();
        }
        catch (Exception ex)
        {
            Erro = MensagemDeErro.Para(ex, "preparar o sistema");
        }
        finally
        {
            Ocupado = false;
        }
    }
}
