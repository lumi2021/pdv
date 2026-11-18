using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Pdv.Core.Entities;
using Pdv.Core.Services;

namespace Pdv.Data.Services;

public sealed class DispositivoService(IDbContextFactory<PdvDbContext> factory) : IDispositivoService
{
    // Evita gravar no banco a cada requisição do celular: o "último acesso" é atualizado no máximo uma vez por minuto.
    private static readonly TimeSpan IntervaloDeAcesso = TimeSpan.FromMinutes(1);

    public async Task<ParearResultado> ParearAsync(string nome, CancellationToken ct = default)
    {
        var nomeLimpo = (nome ?? string.Empty).Trim();
        if (nomeLimpo.Length == 0)
            throw new RegraDeNegocioException("Informe o seu nome para conectar o celular.");
        if (nomeLimpo.Length > 60)
            throw new RegraDeNegocioException("O nome é muito longo (máximo de 60 letras).");

        var token = GerarToken();

        using var trava = await EscritaSerializada.EntrarAsync(ct);
        await using var db = await factory.CreateDbContextAsync(ct);

        var dispositivo = new Dispositivo
        {
            Nome = nomeLimpo,
            TokenHash = Hash(token),
            PareadoEm = DateTime.UtcNow
        };
        db.Dispositivos.Add(dispositivo);
        await db.SaveChangesAsync(ct);

        return new ParearResultado(dispositivo, token);
    }

    public async Task<Dispositivo?> AutenticarAsync(string? token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var hash = Hash(token.Trim());

        await using var db = await factory.CreateDbContextAsync(ct);
        var dispositivo = await db.Dispositivos.FirstOrDefaultAsync(d => d.TokenHash == hash && d.Ativo, ct);
        if (dispositivo is null) return null;

        var agora = DateTime.UtcNow;
        if (dispositivo.UltimoAcessoEm is null || agora - dispositivo.UltimoAcessoEm > IntervaloDeAcesso)
        {
            using var trava = await EscritaSerializada.EntrarAsync(ct);
            dispositivo.UltimoAcessoEm = agora;
            await db.SaveChangesAsync(ct);
        }

        return dispositivo;
    }

    public async Task<IReadOnlyList<Dispositivo>> ListarAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.Dispositivos
            .AsNoTracking()
            .Where(d => d.Ativo)
            .OrderByDescending(d => d.PareadoEm)
            .ToListAsync(ct);
    }

    public async Task RevogarAsync(int dispositivoId, CancellationToken ct = default)
    {
        using var trava = await EscritaSerializada.EntrarAsync(ct);
        await using var db = await factory.CreateDbContextAsync(ct);

        var dispositivo = await db.Dispositivos.FirstOrDefaultAsync(d => d.Id == dispositivoId, ct)
                          ?? throw new RegraDeNegocioException("Celular não encontrado.");

        dispositivo.Ativo = false;
        await db.SaveChangesAsync(ct);
    }

    private static string GerarToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    // O token tem 256 bits aleatórios, então SHA-256 simples basta (não é senha escolhida por pessoa).
    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
