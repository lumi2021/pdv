using Microsoft.Data.Sqlite;
using Pdv.Core.Infra;

namespace Pdv.Data;

public enum TipoBackup
{
    /// <summary>Um por dia, feito sozinho enquanto o app está aberto.</summary>
    Automatico,

    /// <summary>Pedido pelo usuário.</summary>
    Manual,

    /// <summary>Feito logo antes de atualizar a estrutura do banco numa versão nova do app.</summary>
    AntesDeAtualizar
}

public sealed record InfoBackup(string Caminho, string Nome, TipoBackup Tipo, DateTime CriadoEm, long Bytes);

/// <summary>
/// Cópias de segurança do banco. Usa a cópia online do SQLite, que é consistente mesmo com o app gravando.
/// Guarda só as mais recentes de cada tipo para não encher o disco.
/// </summary>
public sealed class BackupService(string caminhoDoBanco, string pastaDeBackups) : IAsyncDisposable
{
    private static readonly Dictionary<TipoBackup, (string Prefixo, int Manter)> Regras = new()
    {
        [TipoBackup.Automatico] = ("auto", 14),
        [TipoBackup.Manual] = ("manual", 20),
        [TipoBackup.AntesDeAtualizar] = ("atualizacao", 5)
    };

    private readonly SemaphoreSlim _uma = new(1, 1);
    private CancellationTokenSource? _parar;
    private Task? _ciclo;

    public string Pasta => pastaDeBackups;

    /// <summary>Faz a cópia agora. Devolve o caminho, ou <c>null</c> se o banco ainda não existe.</summary>
    public async Task<string?> FazerAsync(TipoBackup tipo, CancellationToken ct = default)
    {
        await _uma.WaitAsync(ct);
        try
        {
            return await Task.Run(() => Copiar(tipo), ct);
        }
        finally
        {
            _uma.Release();
        }
    }

    /// <summary>Faz o backup automático do dia se ainda não foi feito. Devolve o caminho, ou <c>null</c> se já havia um.</summary>
    public async Task<string?> FazerSeNecessarioAsync(CancellationToken ct = default)
    {
        var hoje = DateTime.Now.ToString("yyyyMMdd");
        if (Directory.Exists(pastaDeBackups)
            && Directory.EnumerateFiles(pastaDeBackups, $"pdv-{Regras[TipoBackup.Automatico].Prefixo}-{hoje}-*.db").Any())
            return null;

        return await FazerAsync(TipoBackup.Automatico, ct);
    }

    public IReadOnlyList<InfoBackup> Listar()
    {
        if (!Directory.Exists(pastaDeBackups)) return [];

        var lista = new List<InfoBackup>();
        foreach (var (tipo, regra) in Regras)
        {
            foreach (var arquivo in Directory.EnumerateFiles(pastaDeBackups, $"pdv-{regra.Prefixo}-*.db"))
            {
                var info = new FileInfo(arquivo);
                lista.Add(new InfoBackup(arquivo, info.Name, tipo, info.LastWriteTime, info.Length));
            }
        }

        return lista.OrderByDescending(b => b.CriadoEm).ToList();
    }

    /// <summary>Liga a rotina que confere de hora em hora se o backup do dia já foi feito.</summary>
    public void Iniciar()
    {
        if (_ciclo is not null) return;

        var parar = _parar = new CancellationTokenSource();
        _ciclo = Task.Run(async () =>
        {
            using var relogio = new PeriodicTimer(TimeSpan.FromHours(1));
            do
            {
                try { await FazerSeNecessarioAsync(parar.Token); }
                catch (OperationCanceledException) { return; }
                catch (Exception ex) { RegistroDeErros.Registrar(ex, "Backup automático"); }
            }
            while (await SeguirAsync(relogio, parar.Token));
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_parar is null) return;

        await _parar.CancelAsync();
        try { if (_ciclo is not null) await _ciclo.WaitAsync(TimeSpan.FromSeconds(5)); }
        catch { /* encerrando */ }

        _parar.Dispose();
        _parar = null;
        _ciclo = null;
    }

    private static async Task<bool> SeguirAsync(PeriodicTimer relogio, CancellationToken ct)
    {
        try { return await relogio.WaitForNextTickAsync(ct); }
        catch (OperationCanceledException) { return false; }
    }

    private string? Copiar(TipoBackup tipo)
    {
        if (!File.Exists(caminhoDoBanco)) return null;

        Directory.CreateDirectory(pastaDeBackups);

        var regra = Regras[tipo];
        var destino = Path.Combine(pastaDeBackups, $"pdv-{regra.Prefixo}-{DateTime.Now:yyyyMMdd-HHmmss}.db");

        // Pooling desligado: ao fechar, o arquivo é solto na hora (no Windows, senão ele ficaria travado).
        var origem = new SqliteConnectionStringBuilder { DataSource = caminhoDoBanco, Mode = SqliteOpenMode.ReadWrite, Pooling = false };
        var copia = new SqliteConnectionStringBuilder { DataSource = destino, Pooling = false };

        try
        {
            using (var de = new SqliteConnection(origem.ToString()))
            using (var para = new SqliteConnection(copia.ToString()))
            {
                de.Open();
                para.Open();
                de.BackupDatabase(para);
            }
        }
        catch
        {
            // Não deixa um arquivo pela metade parecendo um backup bom.
            try { File.Delete(destino); } catch { /* segue */ }
            throw;
        }

        LimparAntigos(regra.Prefixo, regra.Manter);
        return destino;
    }

    private void LimparAntigos(string prefixo, int manter)
    {
        try
        {
            var antigos = Directory.EnumerateFiles(pastaDeBackups, $"pdv-{prefixo}-*.db")
                .OrderByDescending(Path.GetFileName) // o nome tem a data: mais novo primeiro
                .Skip(manter);

            foreach (var arquivo in antigos)
            {
                try { File.Delete(arquivo); } catch { /* em uso: fica para a próxima */ }
            }
        }
        catch
        {
            // limpeza é um extra; nunca atrapalha o backup
        }
    }
}
