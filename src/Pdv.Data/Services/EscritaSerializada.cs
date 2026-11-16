namespace Pdv.Data.Services;

/// <summary>
/// Serializa as operações que lêem o estoque e depois gravam (criar pedido, cancelar, ajustar).
/// Como um único processo é dono do banco, uma trava em memória basta para impedir que dois pedidos
/// simultâneos "vendam" o mesmo ingrediente, e evita erros de SQLITE_BUSY.
/// </summary>
internal static class EscritaSerializada
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static async Task<IDisposable> EntrarAsync(CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        return new Liberador();
    }

    private sealed class Liberador : IDisposable
    {
        private int _liberado;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _liberado, 1) == 0)
                Gate.Release();
        }
    }
}
