using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pdv.Server;

namespace Pdv.Desktop.ViewModels;

/// <summary>Mostra se o servidor do celular está ligado e em que endereço o garçom deve entrar.</summary>
public partial class CelularViewModel : SecaoViewModel, IAtualizavel
{
    private readonly ServidorWeb _servidor;

    public CelularViewModel(ServidorWeb servidor)
        : base("Celular", "📱", "Conectar o celular do garçom")
    {
        _servidor = servidor;
    }

    public ObservableCollection<string> Enderecos { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ComFalha))]
    private bool _ativo;

    [ObservableProperty]
    private string _detalhesErro = string.Empty;

    public bool ComFalha => !Ativo;

    /// <summary>Liga o servidor. Nunca lança: em caso de falha a tela mostra o motivo e o botão de tentar de novo.</summary>
    public async Task IniciarServidorAsync()
    {
        try
        {
            await _servidor.IniciarAsync();
            DetalhesErro = string.Empty;
        }
        catch (Exception ex)
        {
            DetalhesErro = ex.InnerException is null ? ex.Message : $"{ex.Message} ({ex.InnerException.Message})";
        }

        Atualizar();
    }

    /// <summary>O IP do computador pode mudar (Wi-Fi reconectou), então os endereços são relidos ao abrir a tela.</summary>
    public Task AtualizarAsync()
    {
        Atualizar();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task TentarNovamenteAsync() => IniciarServidorAsync();

    private void Atualizar()
    {
        Ativo = _servidor.Ativo;

        Enderecos.Clear();
        foreach (var endereco in _servidor.Enderecos)
            Enderecos.Add(endereco);
    }
}
