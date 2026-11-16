using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Pdv.Data;

/// <summary>Usada só pelas ferramentas do EF (dotnet ef migrations ...), nunca em execução.</summary>
public class PdvDbContextFactory : IDesignTimeDbContextFactory<PdvDbContext>
{
    public PdvDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PdvDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options;

        return new PdvDbContext(options);
    }
}
