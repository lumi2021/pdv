using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Pdv.Data;
using Pdv.Desktop.ViewModels;
using Pdv.Core.Services;
using Pdv.Server;
using Pdv.Desktop.Views;

namespace Pdv.Desktop;

public partial class App : Application
{
    private ServiceProvider? _services;

    /// <summary>Contêiner de injeção de dependência compartilhado pelo app (e, no futuro, pelo servidor web).</summary>
    public static IServiceProvider Services => ((App)Current!)._services
        ?? throw new InvalidOperationException("O app ainda não foi inicializado.");

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var collection = new ServiceCollection();
            ConfigureServices(collection);
            _services = collection.BuildServiceProvider();

            var mainViewModel = _services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow { DataContext = mainViewModel };
            // O servidor web só implementa DisposeAsync; roda fora da thread da interface para não travar o fechamento.
            desktop.Exit += (_, _) => Encerrar();

            // Prepara o banco em segundo plano enquanto a janela já mostra "Preparando o sistema...".
            // IniciarAsync trata os próprios erros e nunca lança.
            _ = mainViewModel.IniciarAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Fecha o app sem nunca mostrar erro ao usuário: no fechamento não há mais o que fazer com uma falha.</summary>
    private void Encerrar()
    {
        try
        {
            // O timer da tela do celular precisa parar na thread da interface, antes de desmontar os serviços.
            _services?.GetService<CelularViewModel>()?.Parar();
            Task.Run(() => _services!.DisposeAsync().AsTask()).Wait(TimeSpan.FromSeconds(10));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Aviso ao fechar o app: {ex}");
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddPdvData();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<EstoqueViewModel>();
        services.AddSingleton<CardapioViewModel>();

        // Servidor web do PDV móvel: usa os mesmos serviços de domínio do app.
        services.AddSingleton(new CodigoPareamento());
        services.AddSingleton(provider => new ServidorWeb(provider));
        services.AddSingleton<CelularViewModel>();
    }
}
