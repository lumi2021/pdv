using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pdv.Core.Infra;
using Pdv.Data;

namespace Pdv.Desktop.ViewModels;

public sealed record BackupLinha(string Titulo, string Detalhe);

/// <summary>Nome do negócio, cópias de segurança e atalhos para as pastas do sistema.</summary>
public partial class AjustesViewModel : SecaoViewModel, IAtualizavel
{
    private readonly IConfiguracaoService _configuracao;
    private readonly BackupService _backup;

    public AjustesViewModel(IConfiguracaoService configuracao, BackupService backup)
        : base("Ajustes", "⚙️", "Nome do negócio e cópias de segurança")
    {
        _configuracao = configuracao;
        _backup = backup;
        _nomeDoNegocio = configuracao.Atual.NomeDoNegocio;
    }

    public ObservableCollection<BackupLinha> Backups { get; } = [];

    /// <summary>Versão do programa (a mesma da tag do release), para quem dá suporte.</summary>
    public string Versao { get; } = LerVersao();

    private static string LerVersao()
    {
        var v = System.Reflection.Assembly.GetExecutingAssembly()
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion;

        // "1.2.3+abcdef" -> "1.2.3"
        return string.IsNullOrWhiteSpace(v) ? "desconhecida" : v.Split('+')[0];
    }

    public string PastaDosDados => DatabasePaths.DataFolder();
    public string PastaDeBackups => _backup.Pasta;

    [ObservableProperty] private string _nomeDoNegocio;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemMensagem))]
    private string _mensagem = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SemBackups))]
    private bool _temBackups;

    [ObservableProperty] private bool _ocupado;

    public bool TemMensagem => Mensagem.Length > 0;
    public bool SemBackups => !TemBackups;

    public Task AtualizarAsync()
    {
        try
        {
            NomeDoNegocio = _configuracao.Atual.NomeDoNegocio;

            Backups.Clear();
            foreach (var b in _backup.Listar().Take(8))
            {
                var tipo = b.Tipo switch
                {
                    TipoBackup.Automatico => "Automático",
                    TipoBackup.Manual => "Manual",
                    _ => "Antes de atualizar"
                };
                Backups.Add(new BackupLinha($"{b.CriadoEm:dd/MM/yyyy HH:mm} · {tipo}", $"{b.Bytes / 1024.0:N0} KB · {b.Nome}"));
            }

            TemBackups = Backups.Count > 0;
        }
        catch (Exception ex)
        {
            Mensagem = MensagemDeErro.Para(ex, "ler as cópias de segurança");
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task SalvarNomeAsync()
    {
        try
        {
            await _configuracao.SalvarAsync(_configuracao.Atual with { NomeDoNegocio = NomeDoNegocio });
            NomeDoNegocio = _configuracao.Atual.NomeDoNegocio;
            Mensagem = "Nome salvo. Ele aparece nas notas em PDF.";
        }
        catch (Exception ex)
        {
            Mensagem = MensagemDeErro.Para(ex, "salvar o nome");
        }
    }

    [RelayCommand]
    private async Task FazerBackupAsync()
    {
        Ocupado = true;
        try
        {
            var caminho = await _backup.FazerAsync(TipoBackup.Manual);
            Mensagem = caminho is null
                ? "Ainda não há dados para guardar."
                : "Cópia de segurança feita com sucesso.";
            await AtualizarAsync();
        }
        catch (Exception ex)
        {
            Mensagem = MensagemDeErro.Para(ex, "fazer a cópia de segurança");
        }
        finally
        {
            Ocupado = false;
        }
    }

    [RelayCommand]
    private void AbrirPastaDeBackups() => AbrirOuAvisar(PastaDeBackups);

    [RelayCommand]
    private void AbrirPastaDeRegistros() =>
        AbrirOuAvisar(RegistroDeErros.Pasta ?? Path.Combine(PastaDosDados, "logs"));

    [RelayCommand]
    private void FecharMensagem() => Mensagem = string.Empty;

    private void AbrirOuAvisar(string pasta)
    {
        if (!Sistema.Abrir(pasta))
            Mensagem = $"Não consegui abrir a pasta. Ela fica em: {pasta}";
    }
}
