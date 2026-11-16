using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Pdv.Data;
using Pdv.Desktop.ViewModels;
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
            desktop.Exit += (_, _) => _services.Dispose();

            // Prepara o banco em segundo plano enquanto a janela já mostra "Preparando o sistema...".
            // IniciarAsync trata os próprios erros e nunca lança.
            _ = mainViewModel.IniciarAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddPdvData();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<EstoqueViewModel>();
    }
}
