namespace Pdv.Core.Entities;

/// <summary>Celular de garçom autorizado a usar o PDV web. Guarda só o hash do token; o token fica no celular.</summary>
public class Dispositivo
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime PareadoEm { get; set; } = DateTime.UtcNow;
    public DateTime? UltimoAcessoEm { get; set; }
    public bool Ativo { get; set; } = true;
}
