using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Pdv.Core.Entities;
using Pdv.Core.Services;

namespace Pdv.Server.Api;

internal static class PdvEndpoints
{
    private static readonly string Versao =
        typeof(PdvEndpoints).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    public static void Mapear(WebApplication app)
    {
        app.Use(TratarErrosAsync(app.Logger));

        app.MapGet("/", () => Results.Content(PaginaInicial.Html, "text/html; charset=utf-8"));

        var api = app.MapGroup("/api");

        api.MapGet("/status", () => new StatusApi("PDV Lanchonete", Versao, DateTime.UtcNow));

        api.MapGet("/cardapio", async (IMenuService menu, CancellationToken ct) =>
        {
            var produtos = await menu.ListarAsync(incluirDesativados: false, ct);
            return produtos
                .Select(p => new ProdutoCardapioApi(p.Id, p.Nome, p.Categoria, p.Preco, p.Disponivel))
                .ToList();
        });

        api.MapGet("/pedidos", async (IPedidoService pedidos, CancellationToken ct) =>
        {
            var fila = await pedidos.ListarFilaAsync(ct);
            return fila.Select(ParaApi).ToList();
        });

        api.MapPost("/pedidos", async (NovoPedidoApi corpo, IPedidoService pedidos, CancellationToken ct) =>
        {
            var itens = (corpo.Itens ?? [])
                .Select(i => new NovoItemPedido(i.ProdutoId, i.Quantidade, i.Observacao))
                .ToList();

            var pedido = await pedidos.CriarAsync(new NovoPedido(corpo.Identificacao, corpo.Observacao, itens), ct);
            return Results.Created($"/api/pedidos/{pedido.Id}", ParaApi(pedido));
        });

        api.MapPost("/pedidos/{id:int}/status", async (int id, AlterarStatusApi corpo, IPedidoService pedidos, CancellationToken ct) =>
        {
            if (corpo.Status == StatusPedido.Cancelado)
                throw new RegraDeNegocioException("Para cancelar um pedido, use a opção de cancelar.");

            return ParaApi(await pedidos.AlterarStatusAsync(id, corpo.Status, ct));
        });

        api.MapPost("/pedidos/{id:int}/cancelar", async (int id, CancelarPedidoApi? corpo, IPedidoService pedidos, CancellationToken ct) =>
        {
            var devolver = corpo?.DevolverEstoque ?? true;
            return ParaApi(await pedidos.CancelarAsync(id, devolver, ct));
        });
    }

    private static PedidoApi ParaApi(Pedido p) => new(
        p.Id,
        p.Numero,
        p.Identificacao,
        p.Observacao,
        p.Status,
        p.Total,
        // O SQLite devolve a data sem fuso; ela foi gravada em UTC, então o celular precisa saber disso.
        DateTime.SpecifyKind(p.CriadoEm, DateTimeKind.Utc),
        p.Itens
            .Select(i => new ItemPedidoApi(i.ProdutoId, i.NomeProduto, i.PrecoUnitario, i.Quantidade, i.Observacao, i.Subtotal))
            .ToList());

    /// <summary>Converte exceções em respostas JSON com mensagem em português; só o inesperado vira erro 500.</summary>
    private static Func<HttpContext, RequestDelegate, Task> TratarErrosAsync(ILogger logger) => async (contexto, proximo) =>
    {
        try
        {
            await proximo(contexto);

            // Erros que o ASP.NET resolve sozinho (JSON quebrado, rota inexistente) saem sem corpo:
            // devolve uma mensagem para o celular poder mostrar.
            if (!contexto.Response.HasStarted
                && contexto.Response.StatusCode >= 400
                && contexto.Response.ContentLength is null
                && contexto.Response.ContentType is null)
            {
                await contexto.Response.WriteAsJsonAsync(new ErroApi(contexto.Response.StatusCode switch
                {
                    StatusCodes.Status404NotFound => "Endereço não encontrado.",
                    StatusCodes.Status405MethodNotAllowed => "Operação não permitida neste endereço.",
                    StatusCodes.Status415UnsupportedMediaType => "Formato não suportado.",
                    < 500 => "Pedido em formato inválido. Atualize a página do celular e tente de novo.",
                    _ => "Erro inesperado no servidor. Tente de novo."
                }));
            }
        }
        catch (OperationCanceledException) when (contexto.RequestAborted.IsCancellationRequested)
        {
            // O celular desistiu da requisição; não há a quem responder.
        }
        catch (Exception ex) when (!contexto.Response.HasStarted)
        {
            var (status, erro) = Traduzir(ex, logger);
            contexto.Response.StatusCode = status;
            await contexto.Response.WriteAsJsonAsync(erro);
        }
    };

    private static (int Status, ErroApi Erro) Traduzir(Exception ex, ILogger logger) => ex switch
    {
        EstoqueInsuficienteException e => (StatusCodes.Status409Conflict, new ErroApi(
            e.Message,
            e.Faltas.Select(f => new FaltaApi(f.Ingrediente, f.Necessario, f.Disponivel, f.Unidade.Abreviacao())).ToList())),

        RegraDeNegocioException e => (StatusCodes.Status400BadRequest, new ErroApi(e.Message)),

        BadHttpRequestException or JsonException =>
            (StatusCodes.Status400BadRequest, new ErroApi("Pedido em formato inválido. Atualize a página do celular e tente de novo.")),

        _ => Inesperado(ex, logger)
    };

    private static (int Status, ErroApi Erro) Inesperado(Exception ex, ILogger logger)
    {
        logger.LogError(ex, "Erro inesperado ao atender uma requisição");
        return (StatusCodes.Status500InternalServerError, new ErroApi("Erro inesperado no servidor. Tente de novo."));
    }
}
