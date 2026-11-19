using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pdv.Core.Entities;
using Pdv.Core.Services;

namespace Pdv.Desktop.ViewModels;

/// <summary>Tela da cozinha: a fila de pedidos em três colunas, atualizada na hora que um pedido chega ou muda.</summary>
public partial class PedidosViewModel : SecaoViewModel, IAtualizavel
{
    private readonly IPedidoService _pedidos;
    private readonly DispatcherTimer _relogio;
    private int _atualizando;
    private bool _precisaAtualizarDeNovo;

    public PedidosViewModel(IPedidoService pedidos, PedidoEventos eventos)
        : base("Pedidos", "🧾", "Fila de pedidos em tempo real")
    {
        _pedidos = pedidos;

        // Avisos chegam de qualquer thread (celular, outra tela): volta para a interface antes de mexer nas listas.
        eventos.Ocorreu += evento => Dispatcher.UIThread.Post(() => _ = AtualizarAsync());

        // Rede de segurança e relógio: refaz a leitura e os "há N min" mesmo sem nenhum aviso.
        _relogio = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _relogio.Tick += async (_, _) => await AtualizarAsync();
        _relogio.Start();
    }

    public ObservableCollection<PedidoCardViewModel> APreparar { get; } = [];
    public ObservableCollection<PedidoCardViewModel> EmPreparo { get; } = [];
    public ObservableCollection<PedidoCardViewModel> Prontos { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemErro))]
    private string _erro = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilaVazia))]
    private int _total;

    public bool TemErro => Erro.Length > 0;
    public bool FilaVazia => Total == 0;

    public void Parar() => _relogio.Stop();

    /// <summary>Relê a fila. Nunca lança. Chamadas simultâneas viram uma leitura a mais, não várias em paralelo.</summary>
    public async Task AtualizarAsync()
    {
        if (Interlocked.Exchange(ref _atualizando, 1) == 1)
        {
            _precisaAtualizarDeNovo = true;
            return;
        }

        try
        {
            do
            {
                _precisaAtualizarDeNovo = false;
                var fila = await _pedidos.ListarFilaAsync();

                Preencher(APreparar, fila, StatusPedido.APreparar);
                Preencher(EmPreparo, fila, StatusPedido.EmPreparo);
                Preencher(Prontos, fila, StatusPedido.Pronto);
                Total = fila.Count;
            } while (_precisaAtualizarDeNovo);
        }
        catch (Exception ex)
        {
            Erro = $"Não foi possível ler a fila de pedidos: {ex.Message}";
        }
        finally
        {
            Interlocked.Exchange(ref _atualizando, 0);
        }
    }

    [RelayCommand]
    private async Task AvancarAsync(PedidoCardViewModel? card)
    {
        if (card is null) return;

        var proximo = card.Status switch
        {
            StatusPedido.APreparar => StatusPedido.EmPreparo,
            StatusPedido.EmPreparo => StatusPedido.Pronto,
            _ => StatusPedido.Entregue
        };

        await ExecutarAsync(() => _pedidos.AlterarStatusAsync(card.Id, proximo));
    }

    [RelayCommand]
    private void PedirCancelamento(PedidoCardViewModel? card)
    {
        if (card is not null) card.ConfirmandoCancelamento = true;
    }

    [RelayCommand]
    private void DesistirDoCancelamento(PedidoCardViewModel? card)
    {
        if (card is not null) card.ConfirmandoCancelamento = false;
    }

    /// <summary>Cancela devolvendo os ingredientes ao estoque (ainda não foram usados ou ainda estão inteiros).</summary>
    [RelayCommand]
    private Task CancelarDevolvendoAsync(PedidoCardViewModel? card) =>
        card is null ? Task.CompletedTask : ExecutarAsync(() => _pedidos.CancelarAsync(card.Id, devolverEstoque: true));

    /// <summary>Cancela sem devolver: a comida já foi feita e os ingredientes foram perdidos.</summary>
    [RelayCommand]
    private Task CancelarSemDevolverAsync(PedidoCardViewModel? card) =>
        card is null ? Task.CompletedTask : ExecutarAsync(() => _pedidos.CancelarAsync(card.Id, devolverEstoque: false));

    [RelayCommand]
    private void FecharErro() => Erro = string.Empty;

    private async Task ExecutarAsync(Func<Task> acao)
    {
        try
        {
            Erro = string.Empty;
            await acao();
        }
        catch (RegraDeNegocioException ex)
        {
            // Por exemplo: o pedido já foi cancelado pelo celular. A lista é relida abaixo e mostra a situação real.
            Erro = ex.Message;
        }
        catch (Exception ex)
        {
            Erro = $"Não foi possível concluir: {ex.Message}";
        }

        await AtualizarAsync();
    }

    private static void Preencher(ObservableCollection<PedidoCardViewModel> coluna, IReadOnlyList<Pedido> fila, StatusPedido status)
    {
        var novos = fila.Where(p => p.Status == status).ToList();

        // Mantém os cartões que já estão na tela (o "confirmar cancelamento" aberto não pode sumir a cada aviso).
        for (var i = coluna.Count - 1; i >= 0; i--)
        {
            if (novos.All(p => p.Id != coluna[i].Id))
                coluna.RemoveAt(i);
        }

        foreach (var pedido in novos)
        {
            if (coluna.All(c => c.Id != pedido.Id))
                coluna.Add(new PedidoCardViewModel(pedido));
        }

        foreach (var card in coluna)
            card.AtualizarTempo();
    }
}
