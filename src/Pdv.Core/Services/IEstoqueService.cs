using Pdv.Core.Entities;

namespace Pdv.Core.Services;

public interface IEstoqueService
{
    /// <summary>Ingredientes ordenados por nome. Os desativados só entram se pedido.</summary>
    Task<IReadOnlyList<Ingrediente>> ListarAsync(bool incluirDesativados = false, CancellationToken ct = default);

    /// <summary>Cadastra um ingrediente. Com saldo inicial maior que zero, registra a entrada correspondente.</summary>
    Task<Ingrediente> CriarIngredienteAsync(
        string nome, UnidadeMedida unidade, decimal estoqueMinimo, decimal saldoInicial = 0, CancellationToken ct = default);

    /// <summary>Altera nome e estoque mínimo. A unidade não muda depois de criada (receitas e histórico dependem dela).</summary>
    Task<Ingrediente> AtualizarIngredienteAsync(
        int ingredienteId, string nome, decimal estoqueMinimo, CancellationToken ct = default);

    /// <summary>Desativa o ingrediente. Recusa se ele ainda é usado na receita de algum produto ativo.</summary>
    Task DesativarAsync(int ingredienteId, CancellationToken ct = default);

    Task ReativarAsync(int ingredienteId, CancellationToken ct = default);

    /// <summary>Compra/reposição: soma ao saldo e registra o movimento.</summary>
    Task<Ingrediente> RegistrarEntradaAsync(int ingredienteId, decimal quantidade, string? observacao = null, CancellationToken ct = default);

    /// <summary>Contagem física: define o saldo e registra a diferença como ajuste.</summary>
    Task<Ingrediente> AjustarSaldoAsync(int ingredienteId, decimal novoSaldo, string? observacao = null, CancellationToken ct = default);

    /// <summary>Ingredientes ativos com saldo igual ou abaixo do mínimo.</summary>
    Task<IReadOnlyList<Ingrediente>> ListarEstoqueBaixoAsync(CancellationToken ct = default);
}
