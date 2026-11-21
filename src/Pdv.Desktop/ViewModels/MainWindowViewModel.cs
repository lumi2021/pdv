using Pdv.Core.Infra;
using Pdv.Core.Services;
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
    [NotifyPropertyChangedFor(nameof(Iniciando), nameof(PrimeiroUso), nameof(Pronto), nameof(ComErro))]
    private EstadoApp _estado = EstadoApp.Iniciando;

    [ObservableProperty]
    private SecaoViewModel? _secaoAtual;

    [ObservableProperty]
    private string _mensagemErro = string.Empty;

    [ObservableProperty]
    private string _detalhesErro = string.Empty;

    [ObservableProperty]
    private AssistenteViewModel? _assistente;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemAviso))]
    private string _aviso = string.Empty;

    public bool TemAviso => Aviso.Length > 0;

    /// <summary>Falha inesperada em qualquer tela: avisa em português simples e o app continua aberto.</summary>
    public void MostrarAviso(string mensagem) => Aviso = mensagem;

    [RelayCommand]
    private void FecharAviso() => Aviso = string.Empty;

    public bool Iniciando => Estado == EstadoApp.Iniciando;
    public bool PrimeiroUso => Estado == EstadoApp.PrimeiroUso;
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

            // Pedidos entregues antes da nota existir ganham a nota agora. Falha aqui não impede de abrir o app.
            try { await services.GetRequiredService<INotaService>().EmitirPendentesAsync(); }
            catch { /* a nota pode ser emitida na próxima abertura */ }

            // Só depois do banco pronto o servidor do celular pode atender pedidos.
            // Falha aqui não impede o uso do app: a seção Celular mostra o problema.
            await services.GetRequiredService<CelularViewModel>().IniciarServidorAsync();

            // Daqui para frente o banco existe: liga a rotina de cópias de segurança.
            services.GetRequiredService<BackupService>().Iniciar();

            var configuracao = services.GetRequiredService<IConfiguracaoService>();
            if (!configuracao.Atual.PrimeiroUsoConcluido)
            {
                Assistente = new AssistenteViewModel(
                    configuracao,
                    services.GetRequiredService<IEstoqueService>(),
                    services.GetRequiredService<IMenuService>(),
                    services.GetRequiredService<CelularViewModel>(),
                    FinalizarInicioAsync);
                await Assistente.PrepararAsync();
                Estado = EstadoApp.PrimeiroUso;
                return;
            }

            await FinalizarInicioAsync();
        }
        catch (Exception ex)
        {
            RegistroDeErros.Registrar(ex, "Iniciar o app");
            MensagemErro = "Não foi possível preparar o banco de dados. Feche o programa e abra de novo. " +
                           $"Seus dados antigos estão protegidos por cópias de segurança em {DatabasePaths.BackupFolder()}. " +
                           "Se o problema continuar, chame quem instalou o sistema e mostre os detalhes abaixo.";
            DetalhesErro = $"Pasta dos dados: {DatabasePaths.DataFolder()}{Environment.NewLine}{Environment.NewLine}{ex}";
            Estado = EstadoApp.Erro;
        }
    }

    /// <summary>Monta o menu lateral e abre o app. Chamado direto ao iniciar ou ao fim do assistente.</summary>
    private Task FinalizarInicioAsync()
    {
        if (Secoes.Count == 0)
            CriarSecoes();

        SecaoAtual ??= Secoes[0];
        Estado = EstadoApp.Pronto;
        return Task.CompletedTask;
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
        Secoes.Add(services.GetRequiredService<NotasViewModel>());
        Secoes.Add(services.GetRequiredService<CelularViewModel>());
        Secoes.Add(services.GetRequiredService<AjustesViewModel>());
    }
}
