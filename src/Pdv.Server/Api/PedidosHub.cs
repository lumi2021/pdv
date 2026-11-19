using Microsoft.AspNetCore.SignalR;

namespace Pdv.Server.Api;

/// <summary>Canal em tempo real com os celulares. Só o servidor fala; o celular só escuta e, ao ouvir, busca a fila.</summary>
public sealed class PedidosHub : Hub
{
    public const string Caminho = "/hubs/pedidos";
    public const string MensagemPedidoMudou = "pedidoMudou";
}
