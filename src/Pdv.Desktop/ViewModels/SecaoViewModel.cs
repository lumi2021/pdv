namespace Pdv.Desktop.ViewModels;

/// <summary>
/// Uma seção do menu lateral. As que ainda não têm tela própria mostram um aviso de
/// "em construção" (passo &gt; 0); as prontas herdam desta classe e trazem a própria tela.
/// </summary>
public class SecaoViewModel(string titulo, string icone, string descricao, int passo = 0) : ViewModelBase
{
    public string Titulo { get; } = titulo;
    public string Icone { get; } = icone;
    public string Descricao { get; } = descricao;
    public string Aviso { get; } = passo > 0 ? $"Esta tela será construída no passo {passo}." : string.Empty;
}
