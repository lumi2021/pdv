using System.Security.Cryptography;
using System.Text;

namespace Pdv.Core.Services;

/// <summary>
/// Código curto, mostrado em QR Code no computador, que autoriza celulares a se parearem.
/// Vale por alguns minutos para quantos celulares forem; é trocado sozinho ao vencer
/// ou depois de várias tentativas erradas seguidas.
/// </summary>
public sealed class CodigoPareamento(TimeProvider? relogio = null, TimeSpan? validade = null, int maximoDeFalhas = 5)
{
    // Sem letras e números que se confundem (0/O, 1/I/L).
    private const string Alfabeto = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int Tamanho = 8;

    private readonly TimeProvider _relogio = relogio ?? TimeProvider.System;
    private readonly TimeSpan _validade = validade ?? TimeSpan.FromMinutes(10);
    private readonly object _trava = new();

    private string _codigo = string.Empty;
    private DateTimeOffset _expiraEm;
    private int _falhas;

    /// <summary>Código vigente; gera outro se o anterior venceu.</summary>
    public (string Codigo, DateTimeOffset ExpiraEm) Atual()
    {
        lock (_trava)
        {
            RenovarSeVencido();
            return (_codigo, _expiraEm);
        }
    }

    /// <summary>Invalida o código atual e gera outro.</summary>
    public (string Codigo, DateTimeOffset ExpiraEm) Renovar()
    {
        lock (_trava)
        {
            Gerar();
            return (_codigo, _expiraEm);
        }
    }

    /// <summary>Confere o que o celular digitou/escaneou (ignora maiúsculas, espaços e hífens).</summary>
    public bool Validar(string? tentativa)
    {
        lock (_trava)
        {
            RenovarSeVencido();

            var normalizada = new string((tentativa ?? string.Empty)
                .Where(c => !char.IsWhiteSpace(c) && c != '-')
                .Select(char.ToUpperInvariant)
                .ToArray());

            var certo = CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(normalizada), Encoding.ASCII.GetBytes(_codigo));

            if (certo)
            {
                _falhas = 0;
                return true;
            }

            // Muitas tentativas erradas: troca o código para impedir adivinhação.
            if (++_falhas >= maximoDeFalhas)
                Gerar();

            return false;
        }
    }

    private void RenovarSeVencido()
    {
        if (_codigo.Length == 0 || _relogio.GetUtcNow() >= _expiraEm)
            Gerar();
    }

    private void Gerar()
    {
        _codigo = RandomNumberGenerator.GetString(Alfabeto, Tamanho);
        _expiraEm = _relogio.GetUtcNow() + _validade;
        _falhas = 0;
    }
}
