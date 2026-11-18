using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pdv.Core.Services;
using Pdv.Data.Services;

namespace Pdv.Data;

public static class DependencyInjection
{
    /// <summary>
    /// Registra o banco SQLite e os serviços de domínio. Usa IDbContextFactory porque o app desktop
    /// (janelas de longa duração) e o servidor web precisam de contextos curtos, um por operação.
    /// </summary>
    public static IServiceCollection AddPdvData(this IServiceCollection services, string? databasePath = null)
    {
        var path = databasePath ?? DatabasePaths.DefaultDatabasePath();

        services.AddDbContextFactory<PdvDbContext>(options =>
            options.UseSqlite($"Data Source={path};Foreign Keys=True;Default Timeout=10"));

        services.AddSingleton<IPedidoService, PedidoService>();
        services.AddSingleton<IEstoqueService, EstoqueService>();
        services.AddSingleton<IMenuService, MenuService>();
        services.AddSingleton<IDispositivoService, DispositivoService>();

        return services;
    }
}
