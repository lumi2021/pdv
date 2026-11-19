using Pdv.Core.Entities;

namespace Pdv.Core.Services;

public enum TipoEventoPedido
{
    Criado,
    StatusAlterado,
    Cancelado
}

/// <summary>O que mudou num pedido. Leve de propósito: quem recebe busca os detalhes se precisar.</summary>
public sealed record PedidoEvento(TipoEventoPedido Tipo, int PedidoId, int Numero, StatusPedido Status);

/// <summary>
/// Canal por onde os serviços avisam que a fila mudou (tela da cozinha, celulares conectados).
/// Quem assina recebe o aviso em qualquer thread e não deve demorar: faça o trabalho pesado em outra tarefa.
/// Uma falha em quem assina nunca atrapalha o pedido, que já foi gravado.
/// </summary>
public sealed class PedidoEventos
{
    public event Action<PedidoEvento>? Ocorreu;

    public void Publicar(PedidoEvento evento)
    {
        var assinantes = Ocorreu?.GetInvocationList();
        if (assinantes is null) return;

        foreach (var assinante in assinantes)
        {
            try { ((Action<PedidoEvento>)assinante)(evento); }
            catch { /* um assinante com defeito não pode impedir os outros */ }
        }
    }
}
