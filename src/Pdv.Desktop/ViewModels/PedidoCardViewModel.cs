using CommunityToolkit.Mvvm.ComponentModel;
using Pdv.Core.Entities;

namespace Pdv.Desktop.ViewModels;

/// <summary>Um pedido na tela da cozinha.</summary>
public partial class PedidoCardViewModel : ObservableObject
{
    public PedidoCardViewModel(Pedido pedido)
    {
        Id = pedido.Id;
        Numero = pedido.Numero;
        Identificacao = pedido.Identificacao;
        Observacao = pedido.Observacao ?? string.Empty;
        Status = pedido.Status;
        CriadoEm = DateTime.SpecifyKind(pedido.CriadoEm, DateTimeKind.Utc);
        Itens = pedido.Itens
            .Select(i => new ItemCardViewModel($"{i.Quantidade}× {i.NomeProduto}", i.Observacao ?? string.Empty))
            .ToList();
    }

    public int Id { get; }
    public int Numero { get; }
    public string Identificacao { get; }
    public string Observacao { get; }
    public StatusPedido Status { get; }
    public DateTime CriadoEm { get; }
    public IReadOnlyList<ItemCardViewModel> Itens { get; }

    public bool TemObservacao => Observacao.Length > 0;

    public string Titulo => $"#{Numero} · {Identificacao}";

    /// <summary>Há quanto tempo o cliente espera. Recalculado a cada atualização da tela.</summary>
    public string Espera
    {
        get
        {
            var minutos = (int)(DateTime.UtcNow - CriadoEm).TotalMinutes;
            return minutos < 1 ? "agora mesmo" : minutos < 60 ? $"há {minutos} min" : $"há {minutos / 60} h {minutos % 60:00} min";
        }
    }

    /// <summary>Passou de 15 minutos sem ficar pronto: destaca para a cozinha.</summary>
    public bool Atrasado => Status != StatusPedido.Pronto && (DateTime.UtcNow - CriadoEm).TotalMinutes >= 15;

    public string TextoAvancar => Status switch
    {
        StatusPedido.APreparar => "Começar a preparar",
        StatusPedido.EmPreparo => "Marcar como pronto",
        _ => "Entregue ao cliente"
    };

    /// <summary>Só o cancelamento de um pedido que nem começou volta tudo ao estoque sem perguntar.</summary>
    public bool IngredientesJaUsados => Status != StatusPedido.APreparar;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NaoConfirmando))]
    private bool _confirmandoCancelamento;

    public bool NaoConfirmando => !ConfirmandoCancelamento;

    public void AtualizarTempo()
    {
        OnPropertyChanged(nameof(Espera));
        OnPropertyChanged(nameof(Atrasado));
    }
}

public sealed record ItemCardViewModel(string Texto, string Observacao)
{
    public bool TemObservacao => Observacao.Length > 0;
}
