namespace EdilManager.Core.Entities;

public class RigaFattura
{
    public int Id { get; set; }
    public int FatturaId { get; set; }
    public Fattura Fattura { get; set; } = null!;

    public string? CodiceArticolo { get; set; }
    public string Descrizione { get; set; } = string.Empty;
    public decimal? Quantita { get; set; }
    public decimal? PrezzoUnitario { get; set; }
    public string? UnitaMisura { get; set; }
    public decimal? Sconto { get; set; }
    public decimal AliquotaIVA { get; set; }
    public decimal PrezzoTotale { get; set; }
}
