using Pdv.Core.Services;

namespace Pdv.Tests;

public class CodigoPareamentoTests
{
    private sealed class RelogioFalso : TimeProvider
    {
        public DateTimeOffset Agora { get; set; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Agora;
    }

    [Fact]
    public void Codigo_tem_oito_caracteres_sem_simbolos_confusos()
    {
        var codigo = new CodigoPareamento().Atual().Codigo;

        Assert.Equal(8, codigo.Length);
        Assert.DoesNotContain(codigo, c => "0O1IL".Contains(c));
    }

    [Fact]
    public void Mesmo_codigo_enquanto_vale_e_outro_depois_de_vencer()
    {
        var relogio = new RelogioFalso();
        var codigos = new CodigoPareamento(relogio, TimeSpan.FromMinutes(10));
        var primeiro = codigos.Atual().Codigo;

        relogio.Agora += TimeSpan.FromMinutes(9);
        Assert.Equal(primeiro, codigos.Atual().Codigo);

        relogio.Agora += TimeSpan.FromMinutes(2);
        Assert.NotEqual(primeiro, codigos.Atual().Codigo);
    }

    [Fact]
    public void Valida_ignorando_maiusculas_espacos_e_hifens()
    {
        var codigos = new CodigoPareamento();
        var codigo = codigos.Atual().Codigo;

        Assert.True(codigos.Validar($" {codigo[..4].ToLowerInvariant()}-{codigo[4..]} "));
        Assert.False(codigos.Validar(null));
    }

    [Fact]
    public void Codigo_vencido_nao_valida()
    {
        var relogio = new RelogioFalso();
        var codigos = new CodigoPareamento(relogio, TimeSpan.FromMinutes(10));
        var codigo = codigos.Atual().Codigo;

        relogio.Agora += TimeSpan.FromMinutes(11);

        Assert.False(codigos.Validar(codigo));
    }

    [Fact]
    public void Troca_o_codigo_depois_de_cinco_tentativas_erradas()
    {
        var codigos = new CodigoPareamento(maximoDeFalhas: 5);
        var original = codigos.Atual().Codigo;

        for (var i = 0; i < 5; i++)
            Assert.False(codigos.Validar("AAAAAAAA"));

        Assert.NotEqual(original, codigos.Atual().Codigo);
        Assert.False(codigos.Validar(original));
    }

    [Fact]
    public void Renovar_invalida_o_codigo_anterior()
    {
        var codigos = new CodigoPareamento();
        var antigo = codigos.Atual().Codigo;

        var novo = codigos.Renovar().Codigo;

        Assert.NotEqual(antigo, novo);
        Assert.True(codigos.Validar(novo));
    }
}
