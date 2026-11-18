using Pdv.Core.Services;
using Pdv.Data.Services;

namespace Pdv.Tests;

public class DispositivoServiceTests
{
    [Fact]
    public async Task Parear_devolve_token_e_guarda_apenas_o_hash()
    {
        using var db = new TestDb();
        var servico = new DispositivoService(db);

        var resultado = await servico.ParearAsync("  Maria  ");

        Assert.Equal("Maria", resultado.Dispositivo.Nome);
        Assert.False(string.IsNullOrWhiteSpace(resultado.Token));
        Assert.NotEqual(resultado.Token, resultado.Dispositivo.TokenHash);

        using var ctx = db.CreateDbContext();
        Assert.DoesNotContain(ctx.Dispositivos.ToList(), d => d.TokenHash == resultado.Token);
    }

    [Fact]
    public async Task Autentica_o_token_certo_e_recusa_desconhecidos_ou_vazios()
    {
        using var db = new TestDb();
        var servico = new DispositivoService(db);
        var par = await servico.ParearAsync("Maria");

        Assert.Equal("Maria", (await servico.AutenticarAsync(par.Token))?.Nome);
        Assert.Null(await servico.AutenticarAsync("token-invalido"));
        Assert.Null(await servico.AutenticarAsync(null));
        Assert.Null(await servico.AutenticarAsync(""));
    }

    [Fact]
    public async Task Revogar_tira_o_acesso_e_some_da_lista()
    {
        using var db = new TestDb();
        var servico = new DispositivoService(db);
        var par = await servico.ParearAsync("Maria");
        await servico.ParearAsync("João");

        await servico.RevogarAsync(par.Dispositivo.Id);

        Assert.Null(await servico.AutenticarAsync(par.Token));
        Assert.Equal(["João"], (await servico.ListarAsync()).Select(d => d.Nome));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Nome_em_branco_e_recusado(string nome)
    {
        using var db = new TestDb();
        var servico = new DispositivoService(db);

        await Assert.ThrowsAsync<RegraDeNegocioException>(() => servico.ParearAsync(nome));
    }
}
