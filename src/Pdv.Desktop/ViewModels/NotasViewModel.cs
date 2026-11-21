using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pdv.Core.Entities;
using Pdv.Core.Infra;
using Pdv.Core.Services;
using Pdv.Desktop.Pdf;

namespace Pdv.Desktop.ViewModels;

public sealed record PeriodoNotas(string Nome, int? Dias);

/// <summary>Linha da lista de notas.</summary>
public sealed class NotaLinhaViewModel(NotaResumo nota)
{
    public int Id { get; } = nota.Id;
    public string Titulo { get; } = $"Nota {nota.Numero:0000}";
    public string Subtitulo { get; } = $"Pedido #{nota.NumeroDoPedido} · {nota.Identificacao}";
    public string Data { get; } = nota.EmitidaEmUtc.ToLocalTime().ToString("dd/MM HH:mm");
    public string Total { get; } = NotaPdf.Moeda(nota.Total);
}

public sealed record ItemDaNotaViewModel(string Texto, string Observacao, string Valor)
{
    public bool TemObservacao => Observacao.Length > 0;
}

public sealed class NotaAbertaViewModel(NotaDetalhe nota)
{
    public NotaDetalhe Nota { get; } = nota;
    public string Titulo { get; } = $"Nota interna nº {nota.Numero:0000}";
    public string Pedido { get; } = $"Pedido #{nota.Conteudo.NumeroDoPedido} · {nota.Conteudo.Identificacao}";
    public string Emissao { get; } = $"Emitida em {nota.EmitidaEmUtc.ToLocalTime():dd/MM/yyyy HH:mm}";
    public string Observacao { get; } = nota.Conteudo.Observacao ?? string.Empty;
    public bool TemObservacao => Observacao.Length > 0;
    public string Total { get; } = NotaPdf.Moeda(nota.Total);
    public string Aviso => NotaAviso.SemValorFiscal;

    public IReadOnlyList<ItemDaNotaViewModel> Itens { get; } = nota.Conteudo.Itens
        .Select(i => new ItemDaNotaViewModel(
            $"{i.Quantidade}× {i.Nome}", i.Observacao ?? string.Empty, NotaPdf.Moeda(i.Subtotal)))
        .ToList();

    public string NomeSugeridoDoPdf => $"Nota-{Nota.Numero:0000}.pdf";
}

/// <summary>Seção Notas: procurar, ver e exportar em PDF as notas internas (sem valor fiscal).</summary>
public partial class NotasViewModel : SecaoViewModel, IAtualizavel
{
    private readonly INotaService _notas;
    private readonly IConfiguracaoService _configuracao;
    private int _versaoDaBusca;

    public NotasViewModel(INotaService notas, PedidoEventos eventos, IConfiguracaoService configuracao)
        : base("Notas", "📄", "Notas internas dos pedidos")
    {
        _notas = notas;
        _configuracao = configuracao;
        _periodoSelecionado = Periodos[0];

        // Pedido entregue = nota nova: a lista se atualiza sozinha.
        eventos.Ocorreu += evento =>
        {
            if (evento.Status == StatusPedido.Entregue)
                Dispatcher.UIThread.Post(() => _ = AtualizarAsync());
        };
    }

    public IReadOnlyList<PeriodoNotas> Periodos { get; } =
    [
        new("Hoje", 0),
        new("Últimos 7 dias", 7),
        new("Últimos 30 dias", 30),
        new("Todas", null)
    ];

    public ObservableCollection<NotaLinhaViewModel> Notas { get; } = [];

    [ObservableProperty] private string _busca = string.Empty;
    [ObservableProperty] private PeriodoNotas _periodoSelecionado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemMensagem))]
    private string _mensagem = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ListaVazia))]
    private bool _carregou;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemDetalhe), nameof(SemDetalhe))]
    private NotaAbertaViewModel? _detalhe;

    [ObservableProperty] private NotaLinhaViewModel? _selecionada;

    public bool TemMensagem => Mensagem.Length > 0;
    public bool ListaVazia => Carregou && Notas.Count == 0;
    public bool TemDetalhe => Detalhe is not null;
    public bool SemDetalhe => Detalhe is null;

    partial void OnBuscaChanged(string value) => _ = AtualizarAsync();
    partial void OnPeriodoSelecionadoChanged(PeriodoNotas value) => _ = AtualizarAsync();

    partial void OnSelecionadaChanged(NotaLinhaViewModel? value) => _ = CarregarDetalheAsync(value);

    /// <summary>Relê a lista. Nunca lança. Se várias leituras se cruzam, só a mais recente vale.</summary>
    public async Task AtualizarAsync()
    {
        var versao = ++_versaoDaBusca;
        try
        {
            DateTime? desde = PeriodoSelecionado.Dias is { } dias
                ? DateTime.Now.Date.AddDays(-dias).ToUniversalTime()
                : null;

            var resultado = await _notas.ListarAsync(Busca, desde);
            if (versao != _versaoDaBusca) return;

            var idAberto = Selecionada?.Id;
            Notas.Clear();
            foreach (var nota in resultado)
                Notas.Add(new NotaLinhaViewModel(nota));

            // Mantém a nota aberta selecionada quando ela continua na lista.
            if (idAberto is { } id)
                Selecionada = Notas.FirstOrDefault(n => n.Id == id);

            Carregou = true;
            OnPropertyChanged(nameof(ListaVazia));
            Mensagem = string.Empty;
        }
        catch (Exception ex)
        {
            Mensagem = MensagemDeErro.Para(ex, "ler as notas");
        }
    }

    private async Task CarregarDetalheAsync(NotaLinhaViewModel? linha)
    {
        if (linha is null)
        {
            Detalhe = null;
            return;
        }

        try
        {
            var nota = await _notas.ObterAsync(linha.Id);
            if (Selecionada?.Id == linha.Id)
                Detalhe = nota is null ? null : new NotaAbertaViewModel(nota);
        }
        catch (Exception ex)
        {
            Mensagem = MensagemDeErro.Para(ex, "abrir a nota");
        }
    }

    /// <summary>Gera o PDF da nota aberta no stream recebido (o arquivo escolhido pelo usuário).</summary>
    public async Task ExportarPdfAsync(Stream destino)
    {
        if (Detalhe is null) return;

        var nota = Detalhe.Nota;
        var nomeDoNegocio = _configuracao.Atual.NomeDoNegocio;
        await Task.Run(() => NotaPdf.Gerar(nota, nomeDoNegocio, destino));
    }

    public void InformarPdfSalvo(string? caminho) =>
        Mensagem = caminho is null ? "PDF salvo." : $"PDF salvo em {caminho}";

    public void InformarFalhaNoPdf(Exception ex) =>
        Mensagem = MensagemDeErro.Para(ex, "salvar o PDF");

    [RelayCommand]
    private void FecharMensagem() => Mensagem = string.Empty;
}
