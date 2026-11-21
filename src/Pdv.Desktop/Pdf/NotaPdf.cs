using System.Globalization;
using Pdv.Core.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Pdv.Desktop.Pdf;

/// <summary>Desenha a nota interna em PDF (meia folha A5).</summary>
public static class NotaPdf
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    static NotaPdf()
    {
        // Licença Community do QuestPDF: gratuita para empresas com faturamento anual abaixo de US$ 1 milhão.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static string Moeda(decimal valor) => valor.ToString("C2", PtBr);

    public static void Gerar(NotaDetalhe nota, Stream destino)
    {
        var emitida = nota.EmitidaEmUtc.ToLocalTime();
        var c = nota.Conteudo;

        Document.Create(documento => documento.Page(pagina =>
        {
            pagina.Size(PageSizes.A5);
            pagina.Margin(28);
            pagina.DefaultTextStyle(x => x.FontSize(11));

            pagina.Header().Column(coluna =>
            {
                coluna.Item().Text("PDV Lanchonete").FontSize(10).FontColor(Colors.Grey.Darken1);
                coluna.Item().Text($"Nota interna nº {nota.Numero:0000}").FontSize(20).SemiBold();
                coluna.Item().PaddingTop(2).Text($"Pedido #{c.NumeroDoPedido} · {c.Identificacao}");
                coluna.Item().Text($"Emitida em {emitida:dd/MM/yyyy HH:mm}").FontColor(Colors.Grey.Darken1);
                coluna.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
            });

            pagina.Content().PaddingVertical(10).Column(coluna =>
            {
                coluna.Item().Table(tabela =>
                {
                    tabela.ColumnsDefinition(def =>
                    {
                        def.RelativeColumn();
                        def.ConstantColumn(32);
                        def.ConstantColumn(62);
                        def.ConstantColumn(68);
                    });

                    tabela.Header(cab =>
                    {
                        cab.Cell().PaddingBottom(4).Text("Item").SemiBold();
                        cab.Cell().PaddingBottom(4).AlignRight().Text("Qtd").SemiBold();
                        cab.Cell().PaddingBottom(4).AlignRight().Text("Unit.").SemiBold();
                        cab.Cell().PaddingBottom(4).AlignRight().Text("Total").SemiBold();
                    });

                    foreach (var item in c.Itens)
                    {
                        tabela.Cell().PaddingVertical(2).Column(celula =>
                        {
                            celula.Item().Text(item.Nome);
                            if (!string.IsNullOrWhiteSpace(item.Observacao))
                                celula.Item().Text(item.Observacao).FontSize(9).Italic().FontColor(Colors.Grey.Darken1);
                        });
                        tabela.Cell().PaddingVertical(2).AlignRight().Text(item.Quantidade.ToString(PtBr));
                        tabela.Cell().PaddingVertical(2).AlignRight().Text(Moeda(item.PrecoUnitario));
                        tabela.Cell().PaddingVertical(2).AlignRight().Text(Moeda(item.Subtotal));
                    }
                });

                coluna.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                coluna.Item().PaddingTop(6).AlignRight().Text($"Total  {Moeda(nota.Total)}").FontSize(15).SemiBold();

                if (!string.IsNullOrWhiteSpace(c.Observacao))
                    coluna.Item().PaddingTop(10).Text($"Observação: {c.Observacao}").FontSize(10);
            });

            pagina.Footer().AlignCenter()
                .Text(NotaAviso.SemValorFiscal).FontSize(9).Italic().FontColor(Colors.Grey.Darken1);
        })).GeneratePdf(destino);
    }
}
