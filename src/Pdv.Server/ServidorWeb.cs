using System.Net;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pdv.Core.Services;
using Pdv.Server.Api;

namespace Pdv.Server;

/// <summary>
/// Servidor web (Kestrel) que roda dentro do próprio app e entrega a API e o PDV do celular na rede local.
/// Usa os serviços de domínio do app, então o banco continua com um único dono.
/// </summary>
public sealed class ServidorWeb(IServiceProvider servicosDoApp, ServidorWebOpcoes? opcoes = null) : IAsyncDisposable
{
    private readonly ServidorWebOpcoes _opcoes = opcoes ?? new ServidorWebOpcoes();
    private readonly SemaphoreSlim _ciclo = new(1, 1);
    private WebApplication? _app;
    private PedidoEventos? _eventos;
    private Action<PedidoEvento>? _assinante;

    public bool Ativo => _app is not null;
    public int Porta { get; private set; }

    /// <summary>Endereços que o celular deve digitar. Recalculados a cada leitura, pois o IP do PC pode mudar.</summary>
    public IReadOnlyList<string> Enderecos
    {
        get
        {
            if (!Ativo) return [];
            if (_opcoes.SomenteLocal) return [$"http://localhost:{Porta}"];

            var ips = RedeLocal.EnderecosIPv4();
            return ips.Count == 0
                ? [$"http://localhost:{Porta}"]
                : ips.Select(ip => $"http://{ip}:{Porta}").ToList();
        }
    }

    /// <summary>Liga o servidor na primeira porta livre. Não faz nada se já estiver ligado.</summary>
    public async Task IniciarAsync(CancellationToken ct = default)
    {
        await _ciclo.WaitAsync(ct);
        try
        {
            if (_app is not null) return;

            var portas = _opcoes.PortaInicial == 0
                ? [0]
                : Enumerable.Range(_opcoes.PortaInicial, Math.Max(1, _opcoes.Tentativas)).ToArray();

            Exception? ultimaFalha = null;
            foreach (var porta in portas)
            {
                var app = Construir(porta);
                try
                {
                    await app.StartAsync(ct);
                    _app = app;
                    Porta = LerPortaReal(app, porta);
                    Assinar(app);
                    return;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    ultimaFalha = ex;
                    await app.DisposeAsync();
                }
            }

            throw new InvalidOperationException(
                $"Não foi possível abrir uma porta de rede (tentei de {portas[0]} a {portas[^1]}).", ultimaFalha);
        }
        finally
        {
            _ciclo.Release();
        }
    }

    public async Task PararAsync()
    {
        await _ciclo.WaitAsync();
        try
        {
            var app = _app;
            if (app is null) return;

            _app = null;
            Porta = 0;
            Desassinar();

            using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try { await app.StopAsync(limite.Token); }
            catch (OperationCanceledException) { /* encerra à força */ }
            await app.DisposeAsync();
        }
        finally
        {
            _ciclo.Release();
        }
    }

    public async ValueTask DisposeAsync() => await PararAsync();

    private WebApplication Construir(int porta)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory
        });

        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.AddServerHeader = false;
            kestrel.Listen(_opcoes.SomenteLocal ? IPAddress.Loopback : IPAddress.Any, porta);
        });

        // O servidor usa as mesmas instâncias de serviço do app.
        builder.Services.AddSingleton(servicosDoApp.GetRequiredService<IMenuService>());
        builder.Services.AddSingleton(servicosDoApp.GetRequiredService<IPedidoService>());
        builder.Services.AddSingleton(servicosDoApp.GetRequiredService<IDispositivoService>());
        builder.Services.AddSingleton(servicosDoApp.GetRequiredService<CodigoPareamento>());
        builder.Services.AddSingleton(servicosDoApp.GetService<PedidoEventos>() ?? new PedidoEventos());

        builder.Services.AddSignalR().AddJsonProtocol(json =>
            json.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        builder.Services.ConfigureHttpJsonOptions(json =>
            json.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        var app = builder.Build();
        PdvEndpoints.Mapear(app);
        return app;
    }

    /// <summary>Repassa cada mudança na fila aos celulares conectados.</summary>
    private void Assinar(WebApplication app)
    {
        var hub = app.Services.GetRequiredService<IHubContext<PedidosHub>>();
        _eventos = app.Services.GetRequiredService<PedidoEventos>();
        _assinante = evento => _ = EnviarAsync(hub, evento);
        _eventos.Ocorreu += _assinante;
    }

    private void Desassinar()
    {
        if (_eventos is not null && _assinante is not null)
            _eventos.Ocorreu -= _assinante;

        _eventos = null;
        _assinante = null;
    }

    private static async Task EnviarAsync(IHubContext<PedidosHub> hub, PedidoEvento evento)
    {
        try
        {
            await hub.Clients.All.SendAsync(
                PedidosHub.MensagemPedidoMudou, new PedidoMudouApi(evento.Tipo.ToString(), evento.PedidoId, evento.Numero, evento.Status));
        }
        catch
        {
            // Sem celular conectado ou conexão caindo: o celular se atualiza sozinho no próximo ciclo.
        }
    }

    private static int LerPortaReal(WebApplication app, int solicitada)
    {
        var enderecos = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses;
        foreach (var endereco in enderecos ?? [])
        {
            if (Uri.TryCreate(endereco, UriKind.Absolute, out var uri) && uri.Port > 0)
                return uri.Port;
        }

        return solicitada;
    }
}
