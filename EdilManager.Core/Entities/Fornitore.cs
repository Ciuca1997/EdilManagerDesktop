namespace EdilManager.Core.Entities;

public class Fornitore
{
    public int Id { get; set; }
    public string PartitaIva { get; set; } = string.Empty;
    public string? CodiceFiscale { get; set; }
    public string Denominazione { get; set; } = string.Empty;
    public string? Indirizzo { get; set; }
    public string? Comune { get; set; }
    public string? Provincia { get; set; }
    public string? Cap { get; set; }
    public string? Email { get; set; }

    public ICollection<Fattura> Fatture { get; set; } = new List<Fattura>();
}
