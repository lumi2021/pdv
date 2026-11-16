namespace Pdv.Core.Entities;

public enum UnidadeMedida
{
    Unidade = 0,
    Grama = 1,
    Mililitro = 2
}

public enum StatusPedido
{
    APreparar = 0,
    EmPreparo = 1,
    Pronto = 2,
    Entregue = 3,
    Cancelado = 4
}

public enum TipoMovimentoEstoque
{
    Entrada = 0,
    BaixaPedido = 1,
    EstornoPedido = 2,
    Ajuste = 3
}
