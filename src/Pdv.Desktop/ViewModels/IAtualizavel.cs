namespace Pdv.Desktop.ViewModels;

/// <summary>
/// Seção que recarrega seus dados toda vez que o usuário abre a tela.
/// A implementação deve tratar os próprios erros e nunca lançar.
/// </summary>
public interface IAtualizavel
{
    Task AtualizarAsync();
}
