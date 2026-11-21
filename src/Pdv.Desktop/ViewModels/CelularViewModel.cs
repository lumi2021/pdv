using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pdv.Core.Infra;
using Pdv.Core.Services;
using Pdv.Server;
using QRCoder;

namespace Pdv.Desktop.ViewModels;

/// <summary>Celular conectado, como aparece na lista da tela.</summary>
public sealed record DispositivoItem(int Id, string Nome, string Descricao);

/// <summary>
/// Liga o servidor do celular e mostra o QR Code de pareamento, o código digitável
/// e os celulares já conectados (que podem ser desconectados).
/// </summary>
public partial class CelularViewModel : SecaoViewModel, IAtualizavel
{
    private readonly ServidorWeb _servidor;
    private readonly CodigoPareamento _codigos;
    private readonly IDispositivoService _dispositivos;
    private DispatcherTimer? _relogio;

    public CelularViewModel(ServidorWeb servidor, CodigoPareamento codigos, IDispositivoService dispositivos)
        : base("Celular", "📱", "Conectar o celular do garçom")
    {
        _servidor = servidor;
        _codigos = codigos;
        _dispositivos = dispositivos;
    }

    public ObservableCollection<string> Enderecos { get; } = [];
    public ObservableCollection<DispositivoItem> Celulares { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ComFalha))]
    private bool _ativo;

    [ObservableProperty]
    private string _detalhesErro = string.Empty;

    [ObservableProperty]
    private Bitmap? _qrCode;

    [ObservableProperty]
    private string _codigo = string.Empty;

    [ObservableProperty]
    private string _validade = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SemCelulares))]
    private bool _temCelulares;

    public bool ComFalha => !Ativo;
    public bool SemCelulares => !TemCelulares;

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
            RegistroDeErros.Registrar(ex, "Iniciar o servidor do celular");
            DetalhesErro = ex.InnerException is null ? ex.Message : $"{ex.Message} ({ex.InnerException.Message})";
        }

        await AtualizarAsync();
        IniciarRelogio();
    }

    /// <summary>O IP do computador pode mudar (Wi-Fi reconectou) e celulares entram a qualquer hora: relê tudo.</summary>
    public async Task AtualizarAsync()
    {
        try
        {
            Ativo = _servidor.Ativo;

            Enderecos.Clear();
            foreach (var endereco in _servidor.Enderecos)
                Enderecos.Add(endereco);

            AtualizarCodigo();
            await AtualizarCelularesAsync();
        }
        catch (Exception ex)
        {
            DetalhesErro = MensagemDeErro.Para(ex, "atualizar os dados do celular");
        }
    }

    [RelayCommand]
    private Task TentarNovamenteAsync() => IniciarServidorAsync();

    [RelayCommand]
    private void NovoCodigo()
    {
        _codigos.Renovar();
        AtualizarCodigo();
    }

    [RelayCommand]
    private async Task DesconectarAsync(DispositivoItem? celular)
    {
        if (celular is null) return;

        try
        {
            await _dispositivos.RevogarAsync(celular.Id);
        }
        catch (Exception ex)
        {
            DetalhesErro = MensagemDeErro.Para(ex, "desconectar o celular");
        }

        await AtualizarCelularesAsync();
    }

    private void AtualizarCodigo()
    {
        if (!_servidor.Ativo || Enderecos.Count == 0)
        {
            _urlDoQr = string.Empty;
            QrCode = null;
            Codigo = string.Empty;
            Validade = string.Empty;
            return;
        }

        var (codigo, expiraEm) = _codigos.Atual();
        var restante = expiraEm - DateTimeOffset.UtcNow;
        Validade = restante.TotalSeconds <= 0
            ? "O código está sendo renovado..."
            : $"O código vale por mais {Math.Max(1, (int)Math.Ceiling(restante.TotalMinutes))} min e depois é trocado sozinho.";

        // O QR só é redesenhado quando o código ou o endereço mudam.
        var url = $"{Enderecos[0]}/#/parear/{codigo}";
        if (url == _urlDoQr) return;

        _urlDoQr = url;
        Codigo = $"{codigo[..4]}-{codigo[4..]}";
        QrCode = GerarQr(url);
    }

    private string _urlDoQr = string.Empty;

    private static Bitmap GerarQr(string conteudo)
    {
        using var gerador = new QRCodeGenerator();
        using var dados = gerador.CreateQrCode(conteudo, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(dados).GetGraphic(10);
        using var memoria = new MemoryStream(png);
        return new Bitmap(memoria);
    }

    private async Task AtualizarCelularesAsync()
    {
        var lista = await _dispositivos.ListarAsync();

        Celulares.Clear();
        foreach (var d in lista)
        {
            var ultimo = d.UltimoAcessoEm is { } quando
                ? $"último acesso em {DateTime.SpecifyKind(quando, DateTimeKind.Utc).ToLocalTime():dd/MM HH:mm}"
                : "ainda não usou";
            Celulares.Add(new DispositivoItem(d.Id, d.Nome, $"conectado em {DateTime.SpecifyKind(d.PareadoEm, DateTimeKind.Utc).ToLocalTime():dd/MM HH:mm}, {ultimo}"));
        }

        TemCelulares = Celulares.Count > 0;
    }

    /// <summary>Para as atualizações periódicas. Chamado ao fechar o app.</summary>
    public void Parar()
    {
        _relogio?.Stop();
        _relogio = null;
    }

    /// <summary>Atualiza a contagem do código e a lista de celulares enquanto o app está aberto.</summary>
    private void IniciarRelogio()
    {
        if (_relogio is not null) return;

        _relogio = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _relogio.Tick += async (_, _) => await AtualizarAsync();
        _relogio.Start();
    }
}
