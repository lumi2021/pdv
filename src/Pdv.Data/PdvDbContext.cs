using Microsoft.EntityFrameworkCore;
using Pdv.Core.Entities;

namespace Pdv.Data;

public class PdvDbContext(DbContextOptions<PdvDbContext> options) : DbContext(options)
{
    public DbSet<Ingrediente> Ingredientes => Set<Ingrediente>();
    public DbSet<Produto> Produtos => Set<Produto>();
    public DbSet<ItemReceita> ItensReceita => Set<ItemReceita>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<ItemPedido> ItensPedido => Set<ItemPedido>();
    public DbSet<MovimentoEstoque> MovimentosEstoque => Set<MovimentoEstoque>();
    public DbSet<NotaInterna> NotasInternas => Set<NotaInterna>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Ingrediente>(e =>
        {
            e.Property(x => x.Nome).HasMaxLength(120).IsRequired();
            e.Property(x => x.Unidade).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Nome).IsUnique();
        });

        modelBuilder.Entity<Produto>(e =>
        {
            e.Property(x => x.Nome).HasMaxLength(120).IsRequired();
            e.Property(x => x.Categoria).HasMaxLength(60);
            e.HasIndex(x => x.Nome).IsUnique();
        });

        modelBuilder.Entity<ItemReceita>(e =>
        {
            e.HasOne(x => x.Produto)
                .WithMany(p => p.Receita)
                .HasForeignKey(x => x.ProdutoId)
                .OnDelete(DeleteBehavior.Cascade);

            // Não permite apagar um ingrediente que ainda é usado em alguma receita.
            e.HasOne(x => x.Ingrediente)
                .WithMany(i => i.UsadoEm)
                .HasForeignKey(x => x.IngredienteId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => new { x.ProdutoId, x.IngredienteId }).IsUnique();
        });

        modelBuilder.Entity<Pedido>(e =>
        {
            e.Property(x => x.Identificacao).HasMaxLength(60);
            e.Property(x => x.Observacao).HasMaxLength(500);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Numero).IsUnique();
            e.HasIndex(x => new { x.Status, x.CriadoEm });
        });

        modelBuilder.Entity<ItemPedido>(e =>
        {
            e.Property(x => x.NomeProduto).HasMaxLength(120).IsRequired();
            e.Property(x => x.Observacao).HasMaxLength(300);

            e.HasOne(x => x.Pedido)
                .WithMany(p => p.Itens)
                .HasForeignKey(x => x.PedidoId)
                .OnDelete(DeleteBehavior.Cascade);

            // Produto com histórico de vendas não pode ser apagado (use Ativo = false).
            e.HasOne(x => x.Produto)
                .WithMany()
                .HasForeignKey(x => x.ProdutoId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MovimentoEstoque>(e =>
        {
            e.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Observacao).HasMaxLength(300);

            e.HasOne(x => x.Ingrediente)
                .WithMany(i => i.Movimentos)
                .HasForeignKey(x => x.IngredienteId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Pedido)
                .WithMany()
                .HasForeignKey(x => x.PedidoId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(x => new { x.IngredienteId, x.CriadoEm });
        });

        modelBuilder.Entity<NotaInterna>(e =>
        {
            e.HasOne(x => x.Pedido)
                .WithMany()
                .HasForeignKey(x => x.PedidoId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => x.PedidoId).IsUnique();
            e.HasIndex(x => x.Numero).IsUnique();
        });
    }
}
