using Pdv.Core.Entities;

namespace Pdv.Core.Services;

/// <param name="Token">Segredo do celular. Só existe neste retorno: o banco guarda apenas o hash.</param>
public sealed record ParearResultado(Dispositivo Dispositivo, string Token);

public interface IDispositivoService
{
    /// <summary>Autoriza um novo celular. A verificação do código de pareamento é feita antes, por quem chama.</summary>
    Task<ParearResultado> ParearAsync(string nome, CancellationToken ct = default);

    /// <summary>Devolve o celular dono do token, ou <c>null</c> se o token for desconhecido ou estiver revogado.</summary>
    Task<Dispositivo?> AutenticarAsync(string? token, CancellationToken ct = default);

    /// <summary>Celulares autorizados, do pareado mais recentemente para o mais antigo.</summary>
    Task<IReadOnlyList<Dispositivo>> ListarAsync(CancellationToken ct = default);

    /// <summary>Tira a autorização do celular; ele precisará ler um novo QR Code.</summary>
    Task RevogarAsync(int dispositivoId, CancellationToken ct = default);
}
