using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Pdv.Data;

namespace Pdv.Desktop.ViewModels;

public partial class MainWindowViewModel(IServiceProvider services) : ViewModelBase
{
    public ObservableCollection<SecaoViewModel> Secoes { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Iniciando), nameof(Pronto), nameof(ComErro))]
    private EstadoApp _estado = EstadoApp.Iniciando;

    [ObservableProperty]
    private SecaoViewModel? _secaoAtual;

    [ObservableProperty]
    private string _mensagemErro = string.Empty;

    [ObservableProperty]
    private string _detalhesErro = string.Empty;

    public bool Iniciando => Estado == EstadoApp.Iniciando;
    public bool Pronto => Estado == EstadoApp.Pronto;
    public bool ComErro => Estado == EstadoApp.Erro;

    /// <summary>
    /// Autoconfiguração: cria a pasta de dados, cria o banco se não existir e aplica as migrations.
    /// Nunca lança; em caso de falha mostra a tela de erro com a opção de tentar de novo.
    /// </summary>
    public async Task IniciarAsync()
    {
        Estado = EstadoApp.Iniciando;

        try
        {
            // Em segundo plano para a janela não congelar na primeira execução.
            await Task.Run(() => DatabaseInitializer.InitializeAsync(services));

            // Só depois do banco pronto o servidor do celular pode atender pedidos.
            // Falha aqui não impede o uso do app: a seção Celular mostra o problema.
            await services.GetRequiredService<CelularViewModel>().IniciarServidorAsync();

            if (Secoes.Count == 0)
                CriarSecoes();

            SecaoAtual ??= Secoes[0];
            Estado = EstadoApp.Pronto;
        }
        catch (Exception ex)
        {
            MensagemErro = "Não foi possível preparar o banco de dados. Feche o programa e abra de novo. " +
                           "Se o problema continuar, chame quem instalou o sistema e mostre os detalhes abaixo.";
            DetalhesErro = $"Pasta dos dados: {DatabasePaths.DataFolder()}{Environment.NewLine}{Environment.NewLine}{ex}";
            Estado = EstadoApp.Erro;
        }
    }

    [RelayCommand]
    private Task TentarNovamenteAsync() => IniciarAsync();

    /// <summary>Ao abrir uma seção, ela recarrega seus dados (estoque mudou por outro caminho, por exemplo).</summary>
    partial void OnSecaoAtualChanged(SecaoViewModel? value)
    {
        if (value is IAtualizavel atualizavel)
            _ = atualizavel.AtualizarAsync();
    }

    private void CriarSecoes()
    {
        Secoes.Add(services.GetRequiredService<PedidosViewModel>());
        Secoes.Add(services.GetRequiredService<EstoqueViewModel>());
        Secoes.Add(services.GetRequiredService<CardapioViewModel>());
        Secoes.Add(new SecaoViewModel("Notas", "📄", "Notas internas dos pedidos", passo: 10));
        Secoes.Add(services.GetRequiredService<CelularViewModel>());
    }
}
