using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Pdv.Data;

public static class DatabaseInitializer
{
    /// <summary>
    /// Deixa o banco pronto para uso: cria a pasta e o arquivo se não existirem
    /// e aplica as migrations pendentes. Seguro para chamar em toda inicialização.
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        var factory = services.GetRequiredService<IDbContextFactory<PdvDbContext>>();
        await using var db = await factory.CreateDbContextAsync(ct);

        EnsureDirectoryFor(db.Database.GetConnectionString());

        // Guarda para desenvolvimento: sem migration o Migrate não cria nenhuma tabela.
        if (!db.Database.GetMigrations().Any())
            throw new InvalidOperationException(
                "Nenhuma migration encontrada no Pdv.Data. Rode: ./scripts/nova-migration.sh Inicial (ou .ps1 no Windows).");

#if DEBUG
        // Só em desenvolvimento: entidades mudaram e falta gerar a migration. Dá o recado claro
        // em vez do erro de SQL. No app instalado as migrations já vêm prontas dentro dele.
        if (db.Database.HasPendingModelChanges())
            throw new InvalidOperationException(
                "O modelo do banco mudou e falta a migration. Rode: ./scripts/nova-migration.sh NomeDaMudanca " +
                "(ou .ps1 no Windows) e abra o app de novo.");
#endif

        // Versão nova do app com mudanças no banco: guarda uma cópia antes de mexer. Se a cópia falhar,
        // o erro vai para o registro e a atualização segue (travar a abertura seria pior para quem usa).
        var temDadosAnteriores = (await db.Database.GetAppliedMigrationsAsync(ct)).Any();
        var temMigrationNova = (await db.Database.GetPendingMigrationsAsync(ct)).Any();
        if (temDadosAnteriores && temMigrationNova)
        {
            try
            {
                if (services.GetService<BackupService>() is { } backup)
                    await backup.FazerAsync(TipoBackup.AntesDeAtualizar, ct);
            }
            catch (Exception ex)
            {
                Pdv.Core.Infra.RegistroDeErros.Registrar(ex, "Backup antes de atualizar o banco");
            }
        }

        await db.Database.MigrateAsync(ct);

        // WAL: leituras não bloqueiam escritas (app e servidor web usam o mesmo arquivo)
        // e o modo é gravado no próprio arquivo do banco.
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", ct);
    }

    private static void EnsureDirectoryFor(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString);
        var dir = Path.GetDirectoryName(Path.GetFullPath(builder.DataSource));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }
}
