using Microsoft.Data.Sqlite;
using Pdv.Data;

namespace Pdv.Tests;

public sealed class BackupServiceTests : IDisposable
{
    private readonly string _pasta = Path.Combine(Path.GetTempPath(), "pdv-backup-" + Guid.NewGuid().ToString("N"));
    private string Banco => Path.Combine(_pasta, "pdv.db");
    private string Backups => Path.Combine(_pasta, "backups");

    public BackupServiceTests() => Directory.CreateDirectory(_pasta);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_pasta, recursive: true); } catch { /* arquivo ainda em uso no Windows: lixo na pasta temporária */ }
    }

    private void CriarBancoComDados(int linhas)
    {
        using var con = new SqliteConnection($"Data Source={Banco};Pooling=False");
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; CREATE TABLE T (Id INTEGER PRIMARY KEY, Nome TEXT);" +
                          string.Concat(Enumerable.Range(1, linhas).Select(i => $"INSERT INTO T (Nome) VALUES ('linha {i}');"));
        cmd.ExecuteNonQuery();
    }

    private static long Contar(string caminho)
    {
        using var con = new SqliteConnection($"Data Source={caminho};Mode=ReadOnly;Pooling=False");
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM T";
        return (long)cmd.ExecuteScalar()!;
    }

    [Fact]
    public async Task Backup_e_uma_copia_completa_do_banco()
    {
        CriarBancoComDados(25);
        await using var servico = new BackupService(Banco, Backups);

        var caminho = await servico.FazerAsync(TipoBackup.Manual);

        Assert.NotNull(caminho);
        Assert.StartsWith(Backups, caminho);
        Assert.Equal(25, Contar(caminho));
    }

    [Fact]
    public async Task Sem_banco_ainda_nao_ha_o_que_copiar()
    {
        await using var servico = new BackupService(Banco, Backups);

        Assert.Null(await servico.FazerAsync(TipoBackup.Automatico));
        Assert.Empty(servico.Listar());
    }

    [Fact]
    public async Task Backup_do_dia_so_e_feito_uma_vez()
    {
        CriarBancoComDados(1);
        await using var servico = new BackupService(Banco, Backups);

        Assert.NotNull(await servico.FazerSeNecessarioAsync());
        Assert.Null(await servico.FazerSeNecessarioAsync());
        Assert.Single(servico.Listar());
    }

    [Fact]
    public async Task Guarda_so_os_14_automaticos_mais_recentes()
    {
        CriarBancoComDados(1);
        Directory.CreateDirectory(Backups);
        for (var i = 1; i <= 20; i++)
            File.WriteAllText(Path.Combine(Backups, $"pdv-auto-2020010{i % 10}-0000{i:00}.db"), "antigo");
        await using var servico = new BackupService(Banco, Backups);

        var novo = await servico.FazerAsync(TipoBackup.Automatico);

        var automaticos = Directory.GetFiles(Backups, "pdv-auto-*.db");
        Assert.Equal(14, automaticos.Length);
        Assert.Contains(novo, automaticos); // o recém-criado nunca é apagado
    }

    [Fact]
    public async Task Limpeza_de_um_tipo_nao_apaga_os_outros()
    {
        CriarBancoComDados(1);
        await using var servico = new BackupService(Banco, Backups);
        await servico.FazerAsync(TipoBackup.Manual);
        await servico.FazerAsync(TipoBackup.AntesDeAtualizar);

        var tipos = servico.Listar().Select(b => b.Tipo).ToHashSet();

        Assert.Contains(TipoBackup.Manual, tipos);
        Assert.Contains(TipoBackup.AntesDeAtualizar, tipos);
    }

    [Fact]
    public async Task Rotina_automatica_cria_o_backup_do_dia_e_para_ao_encerrar()
    {
        CriarBancoComDados(1);
        var servico = new BackupService(Banco, Backups);

        servico.Iniciar();
        for (var i = 0; i < 50 && servico.Listar().Count == 0; i++)
            await Task.Delay(100);

        Assert.Single(servico.Listar());
        await servico.DisposeAsync(); // não pode ficar preso esperando a próxima hora
    }
}
