using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Pdv.Desktop.ViewModels;

namespace Pdv.Desktop.Views;

public partial class NotasView : UserControl
{
    public NotasView()
    {
        InitializeComponent();
    }

    // A caixa "Salvar como" precisa da janela, por isso este trecho fica na tela e não no ViewModel.
    private async void ExportarPdf_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not NotasViewModel vm || vm.Detalhe is not { } nota) return;
        if (TopLevel.GetTopLevel(this) is not { } janela) return;

        string? caminho = null;
        try
        {
            var arquivo = await janela.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Salvar nota em PDF",
                SuggestedFileName = nota.NomeSugeridoDoPdf,
                DefaultExtension = "pdf",
                FileTypeChoices = [new FilePickerFileType("PDF") { Patterns = ["*.pdf"] }]
            });
            if (arquivo is null) return;

            await using (var stream = await arquivo.OpenWriteAsync())
            {
                if (stream.CanSeek) stream.SetLength(0); // sobrescrever um arquivo maior não pode deixar sobra
                await vm.ExportarPdfAsync(stream);
            }

            caminho = arquivo.TryGetLocalPath();
            vm.InformarPdfSalvo(caminho);
        }
        catch (Exception ex)
        {
            vm.InformarFalhaNoPdf(ex);
            return;
        }

        // Abre o PDF no leitor padrão; se não der, o aviso com o caminho já basta.
        try
        {
            if (caminho is not null)
                Process.Start(new ProcessStartInfo(caminho) { UseShellExecute = true });
        }
        catch
        {
            // sem leitor de PDF instalado
        }
    }
}
