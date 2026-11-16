namespace Pdv.Core.Entities;

public class Ingrediente
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public UnidadeMedida Unidade { get; set; } = UnidadeMedida.Unidade;

    /// <summary>Saldo atual em estoque, na unidade do ingrediente.</summary>
    public decimal QuantidadeAtual { get; set; }

    /// <summary>Abaixo (ou igual) deste valor, o app mostra alerta de estoque baixo.</summary>
    public decimal EstoqueMinimo { get; set; }

    public bool Ativo { get; set; } = true;

    public bool EstoqueBaixo => QuantidadeAtual <= EstoqueMinimo;

    public List<ItemReceita> UsadoEm { get; set; } = [];
    public List<MovimentoEstoque> Movimentos { get; set; } = [];
}
