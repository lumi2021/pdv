namespace Pdv.Core.Services;

public interface IMenuService
{
    /// <summary>Produtos ordenados por categoria e nome, já com a disponibilidade calculada pelo estoque.</summary>
    Task<IReadOnlyList<ProdutoMenuDto>> ListarAsync(bool incluirDesativados = false, CancellationToken ct = default);

    /// <summary>Cria ou altera o produto e sincroniza a receita (ingredientes e quantidades por unidade vendida).</summary>
    Task<ProdutoMenuDto> SalvarAsync(ProdutoEdicao produto, CancellationToken ct = default);

    /// <summary>Tira o produto do cardápio sem apagar o histórico de vendas.</summary>
    Task DesativarAsync(int produtoId, CancellationToken ct = default);

    Task ReativarAsync(int produtoId, CancellationToken ct = default);
}
