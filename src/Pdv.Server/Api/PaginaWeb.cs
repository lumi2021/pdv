using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Pdv.Server.Api;

/// <summary>Serve o PDV web do garçom (HTML, CSS e JS embutidos no assembly). Público: não contém dados.</summary>
internal static class PaginaWeb
{
    private sealed record Arquivo(byte[] Conteudo, string Tipo);

    private static readonly Lazy<IReadOnlyDictionary<string, Arquivo>> Arquivos = new(Carregar);

    public static void Mapear(WebApplication app)
    {
        app.MapGet("/", (HttpContext contexto) => Servir(contexto, "index.html"));
        app.MapGet("/{arquivo}", (string arquivo, HttpContext contexto) => Servir(contexto, arquivo));
    }

    private static IResult Servir(HttpContext contexto, string nome)
    {
        if (!Arquivos.Value.TryGetValue(nome, out var arquivo))
            return Results.NotFound();

        // Sempre revalida: ao atualizar o app, o celular pega a versão nova sem limpar cache.
        contexto.Response.Headers.CacheControl = "no-cache";
        return Results.Bytes(arquivo.Conteudo, arquivo.Tipo);
    }

    private static IReadOnlyDictionary<string, Arquivo> Carregar()
    {
        var assembly = typeof(PaginaWeb).Assembly;
        var resultado = new Dictionary<string, Arquivo>(StringComparer.Ordinal);

        foreach (var recurso in assembly.GetManifestResourceNames().Where(n => n.StartsWith("web/", StringComparison.Ordinal)))
        {
            var nome = recurso["web/".Length..];
            using var stream = assembly.GetManifestResourceStream(recurso)!;
            using var memoria = new MemoryStream();
            stream.CopyTo(memoria);
            resultado[nome] = new Arquivo(memoria.ToArray(), TipoDe(nome));
        }

        return resultado;
    }

    private static string TipoDe(string nome) => Path.GetExtension(nome).ToLowerInvariant() switch
    {
        ".html" => "text/html; charset=utf-8",
        ".js" => "text/javascript; charset=utf-8",
        ".css" => "text/css; charset=utf-8",
        ".webmanifest" => "application/manifest+json",
        ".svg" => "image/svg+xml",
        ".png" => "image/png",
        _ => "application/octet-stream"
    };
}
