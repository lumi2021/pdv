namespace Pdv.Core.Entities;

public static class UnidadeMedidaExtensions
{
    public static string Abreviacao(this UnidadeMedida unidade) => unidade switch
    {
        UnidadeMedida.Grama => "g",
        UnidadeMedida.Mililitro => "ml",
        _ => "un"
    };
}
