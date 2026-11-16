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
                "Nenhuma migration encontrada no Pdv.Data. Rode: dotnet ef migrations add Inicial --project src/Pdv.Data");

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
