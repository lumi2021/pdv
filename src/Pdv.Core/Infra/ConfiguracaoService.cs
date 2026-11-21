using System.Text.Json;

namespace Pdv.Core.Infra;

/// <summary>Ajustes do app que não são dados do negócio. Ficam num arquivo JSON ao lado do banco.</summary>
public sealed record ConfiguracaoApp(string NomeDoNegocio = ConfiguracaoApp.NomePadrao, bool PrimeiroUsoConcluido = false)
{
    public const string NomePadrao = "PDV Lanchonete";
    public const int TamanhoMaximoDoNome = 60;
}

public interface IConfiguracaoService
{
    ConfiguracaoApp Atual { get; }

    /// <summary>Grava os ajustes. O nome é aparado; vazio volta ao padrão.</summary>
    Task SalvarAsync(ConfiguracaoApp nova, CancellationToken ct = default);
}

public sealed class ConfiguracaoService(string caminhoDoArquivo) : IConfiguracaoService
{
    private static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly SemaphoreSlim _escrita = new(1, 1);
    private ConfiguracaoApp? _atual;

    public ConfiguracaoApp Atual => _atual ??= Ler();

    public async Task SalvarAsync(ConfiguracaoApp nova, CancellationToken ct = default)
    {
        var nome = (nova.NomeDoNegocio ?? string.Empty).Trim();
        if (nome.Length > ConfiguracaoApp.TamanhoMaximoDoNome)
            nome = nome[..ConfiguracaoApp.TamanhoMaximoDoNome].TrimEnd();

        var limpa = nova with { NomeDoNegocio = nome.Length == 0 ? ConfiguracaoApp.NomePadrao : nome };

        await _escrita.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(caminhoDoArquivo))!);

            // Grava num arquivo temporário e troca: queda de energia no meio não deixa o arquivo pela metade.
            var temporario = caminhoDoArquivo + ".tmp";
            await File.WriteAllTextAsync(temporario, JsonSerializer.Serialize(limpa, Opcoes), ct);
            File.Move(temporario, caminhoDoArquivo, overwrite: true);

            _atual = limpa;
        }
        finally
        {
            _escrita.Release();
        }
    }

    private ConfiguracaoApp Ler()
    {
        if (!File.Exists(caminhoDoArquivo))
            return new ConfiguracaoApp();

        try
        {
            var lida = JsonSerializer.Deserialize<ConfiguracaoApp>(File.ReadAllText(caminhoDoArquivo), Opcoes);
            if (lida is not null)
                return string.IsNullOrWhiteSpace(lida.NomeDoNegocio) ? lida with { NomeDoNegocio = ConfiguracaoApp.NomePadrao } : lida;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            RegistroDeErros.Registrar(ex, "Ler configuração");
        }

        // Arquivo ilegível: guarda de lado (para não perder) e segue com o padrão.
        try { File.Move(caminhoDoArquivo, caminhoDoArquivo + ".quebrado", overwrite: true); } catch { /* sem permissão: segue */ }
        return new ConfiguracaoApp();
    }
}
