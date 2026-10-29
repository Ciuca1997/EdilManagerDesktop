namespace EdilManager.Core.Entities;

public class Dipendente
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Cognome { get; set; } = string.Empty;
    public string CodiceFiscale { get; set; } = string.Empty;
    public DateOnly DataNascita { get; set; }
    public DateOnly DataAssunzione { get; set; }
    public string? Mansione { get; set; }
    public string? Livello { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }

    public ICollection<Presenza> Presenze { get; set; } = new List<Presenza>();
}
