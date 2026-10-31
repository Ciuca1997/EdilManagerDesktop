namespace EdilManager.Core.Entities;

public class Cantiere
{
    public int Id { get; set; }
    public string CUP { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;

    // Cantiere creato dall'utente senza CUP: il CUP è un codice interno (INT-0001...) che non va mostrato come un CUP vero.
    public bool Interno { get; set; }

    public ICollection<Fattura> Fatture { get; set; } = new List<Fattura>();
    public ICollection<Presenza> Presenze { get; set; } = new List<Presenza>();
}
