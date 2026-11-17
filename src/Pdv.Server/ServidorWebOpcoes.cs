namespace Pdv.Server;

/// <param name="PortaInicial">Primeira porta tentada. Se estiver ocupada, tenta a seguinte. 0 = qualquer porta livre.</param>
/// <param name="Tentativas">Quantas portas seguidas tentar a partir da inicial.</param>
/// <param name="SomenteLocal">true = só aceita conexões deste computador (usado nos testes).</param>
public sealed record ServidorWebOpcoes(int PortaInicial = 5080, int Tentativas = 20, bool SomenteLocal = false);
