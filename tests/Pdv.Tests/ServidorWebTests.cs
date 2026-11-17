using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Pdv.Core.Services;
using Pdv.Data.Services;
using Pdv.Server;

namespace Pdv.Tests;

public class ServidorWebTests
{
    /// <summary>Servidor real em porta livre, falando com os serviços reais sobre um SQLite em memória.</summary>
    private sealed class Contexto : IAsyncDisposable
    {
        public required TestDb Db { get; init; }
        public required TestDb.Ids Ids { get; init; }
        public required ServidorWeb Servidor { get; init; }
        public required HttpClient Http { get; init; }

        public static async Task<Contexto> CriarAsync(decimal pao = 10, decimal carne = 1000)
        {
            var db = new TestDb();
            var ids = db.Seed(pao, carne);
            var servidor = new ServidorWeb(Servicos(db), new ServidorWebOpcoes(PortaInicial: 0, SomenteLocal: true));
            await servidor.IniciarAsync();

            return new Contexto
            {
                Db = db,
                Ids = ids,
                Servidor = servidor,
                Http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{servidor.Porta}") }
            };
        }

        public Task<HttpResponseMessage> CriarPedidoAsync(int quantidade, string identificacao = "Mesa 1") =>
            Http.PostAsJsonAsync("/api/pedidos", new
            {
                identificacao,
                observacao = (string?)null,
                itens = new[] { new { produtoId = Ids.XBurger, quantidade, observacao = (string?)null } }
            });

        public async ValueTask DisposeAsync()
        {
            Http.Dispose();
            await Servidor.DisposeAsync();
            Db.Dispose();
        }
    }

    private static IServiceProvider Servicos(TestDb db) =>
        new ServiceCollection()
            .AddSingleton<IMenuService>(new MenuService(db))
            .AddSingleton<IPedidoService>(new PedidoService(db))
            .BuildServiceProvider();

    private static async Task<JsonElement> Json(HttpResponseMessage resposta) =>
        await resposta.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task Status_responde()
    {
        await using var ctx = await Contexto.CriarAsync();

        var resposta = await ctx.Http.GetAsync("/api/status");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("PDV Lanchonete", (await Json(resposta)).GetProperty("nome").GetString());
    }

    [Fact]
    public async Task Cardapio_traz_disponibilidade_sem_expor_a_receita()
    {
        await using var ctx = await Contexto.CriarAsync(pao: 10, carne: 1000);

        var produtos = await Json(await ctx.Http.GetAsync("/api/cardapio"));

        var item = Assert.Single(produtos.EnumerateArray());
        Assert.Equal("X-Burger", item.GetProperty("nome").GetString());
        Assert.Equal(6, item.GetProperty("disponivel").GetInt32());
        Assert.False(item.TryGetProperty("receita", out _));
    }

    [Fact]
    public async Task Criar_pedido_baixa_estoque_e_aparece_na_fila()
    {
        await using var ctx = await Contexto.CriarAsync();

        var resposta = await ctx.CriarPedidoAsync(2);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        Assert.NotNull(resposta.Headers.Location);

        var pedido = await Json(resposta);
        Assert.Equal(1, pedido.GetProperty("numero").GetInt32());
        Assert.Equal("APreparar", pedido.GetProperty("status").GetString());
        Assert.Equal(40m, pedido.GetProperty("total").GetDecimal());
        Assert.EndsWith("Z", pedido.GetProperty("criadoEm").GetString());

        Assert.Equal(700m, ctx.Db.Saldo(ctx.Ids.Carne));

        var fila = await Json(await ctx.Http.GetAsync("/api/pedidos"));
        Assert.Single(fila.EnumerateArray());
    }

    [Fact]
    public async Task Estoque_insuficiente_responde_409_com_mensagem_e_nao_altera_nada()
    {
        await using var ctx = await Contexto.CriarAsync(pao: 10, carne: 1000);

        var resposta = await ctx.CriarPedidoAsync(7); // 1050 g de carne

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
        var erro = await Json(resposta);
        Assert.Contains("Carne", erro.GetProperty("mensagem").GetString());
        Assert.Single(erro.GetProperty("faltas").EnumerateArray());
        Assert.Equal(1000m, ctx.Db.Saldo(ctx.Ids.Carne));
    }

    [Fact]
    public async Task Pedido_invalido_responde_400_com_mensagem()
    {
        await using var ctx = await Contexto.CriarAsync();

        var semItens = await ctx.Http.PostAsJsonAsync("/api/pedidos", new { identificacao = "Mesa 1", itens = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.BadRequest, semItens.StatusCode);
        Assert.Contains("pelo menos um item", (await Json(semItens)).GetProperty("mensagem").GetString());

        var jsonQuebrado = await ctx.Http.PostAsync("/api/pedidos", new StringContent("{ nao e json", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, jsonQuebrado.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace((await Json(jsonQuebrado)).GetProperty("mensagem").GetString()));
    }

    [Fact]
    public async Task Status_segue_a_ordem_e_recusa_saltos_e_cancelamento_por_esta_rota()
    {
        await using var ctx = await Contexto.CriarAsync();
        var id = (await Json(await ctx.CriarPedidoAsync(1))).GetProperty("id").GetInt32();

        var emPreparo = await ctx.Http.PostAsJsonAsync($"/api/pedidos/{id}/status", new { status = "EmPreparo" });
        Assert.Equal(HttpStatusCode.OK, emPreparo.StatusCode);
        Assert.Equal("EmPreparo", (await Json(emPreparo)).GetProperty("status").GetString());

        var volta = await ctx.Http.PostAsJsonAsync($"/api/pedidos/{id}/status", new { status = "APreparar" });
        Assert.Equal(HttpStatusCode.BadRequest, volta.StatusCode);

        var cancelaPorStatus = await ctx.Http.PostAsJsonAsync($"/api/pedidos/{id}/status", new { status = "Cancelado" });
        Assert.Equal(HttpStatusCode.BadRequest, cancelaPorStatus.StatusCode);

        var inexistente = await ctx.Http.PostAsJsonAsync("/api/pedidos/999/status", new { status = "EmPreparo" });
        Assert.Equal(HttpStatusCode.BadRequest, inexistente.StatusCode);
    }

    [Fact]
    public async Task Cancelar_devolve_o_estoque()
    {
        await using var ctx = await Contexto.CriarAsync();
        var id = (await Json(await ctx.CriarPedidoAsync(2))).GetProperty("id").GetInt32();
        Assert.Equal(700m, ctx.Db.Saldo(ctx.Ids.Carne));

        var resposta = await ctx.Http.PostAsync($"/api/pedidos/{id}/cancelar", null);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("Cancelado", (await Json(resposta)).GetProperty("status").GetString());
        Assert.Equal(1000m, ctx.Db.Saldo(ctx.Ids.Carne));
    }

    [Fact]
    public async Task Rota_inexistente_responde_404_com_mensagem()
    {
        await using var ctx = await Contexto.CriarAsync();

        var resposta = await ctx.Http.GetAsync("/api/naoexiste");

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace((await Json(resposta)).GetProperty("mensagem").GetString()));
    }

    [Fact]
    public async Task Ao_parar_o_servidor_deixa_de_responder()
    {
        await using var ctx = await Contexto.CriarAsync();

        await ctx.Servidor.PararAsync();

        Assert.False(ctx.Servidor.Ativo);
        await Assert.ThrowsAsync<HttpRequestException>(() => ctx.Http.GetAsync("/api/status"));
    }

    [Fact]
    public async Task Porta_ocupada_faz_o_servidor_usar_a_seguinte()
    {
        using var db = new TestDb();
        db.Seed();

        var ocupante = new TcpListener(IPAddress.Loopback, 0);
        ocupante.Start();
        try
        {
            var ocupada = ((IPEndPoint)ocupante.LocalEndpoint).Port;
            await using var servidor = new ServidorWeb(
                Servicos(db), new ServidorWebOpcoes(PortaInicial: ocupada, Tentativas: 10, SomenteLocal: true));

            await servidor.IniciarAsync();

            Assert.True(servidor.Ativo);
            Assert.InRange(servidor.Porta, ocupada + 1, ocupada + 9);
        }
        finally
        {
            ocupante.Stop();
        }
    }

    [Theory]
    [InlineData("192.168.0.12", true)]
    [InlineData("10.1.2.3", true)]
    [InlineData("172.16.0.1", true)]
    [InlineData("172.31.255.1", true)]
    [InlineData("172.32.0.1", false)]
    [InlineData("8.8.8.8", false)]
    [InlineData("169.254.1.1", false)]
    public void Reconhece_faixas_de_rede_privada(string ip, bool esperado) =>
        Assert.Equal(esperado, RedeLocal.EhEnderecoPrivado(IPAddress.Parse(ip)));
}
