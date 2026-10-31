namespace EdilManager.Core.Entities;

public class Fattura
{
    public int Id { get; set; }
    public string NumeroDocumento { get; set; } = string.Empty;
    public DateOnly DataDocumento { get; set; }
    public string TipoDocumento { get; set; } = string.Empty;
    public decimal TotaleImponibile { get; set; }
    public decimal TotaleImposta { get; set; }
    public decimal TotaleDocumento { get; set; }
    public string? ModalitaPagamento { get; set; }
    public DateOnly? DataScadenzaPagamento { get; set; }
    public decimal? ImportoPagamento { get; set; }

    // Stato di pagamento (la scadenza «Scaduta» si calcola); DataPagamento ha senso solo se Pagata.
    public StatoPagamento Stato { get; set; }
    public DateOnly? DataPagamento { get; set; }

    public int FornitoreId { get; set; }
    public Fornitore Fornitore { get; set; } = null!;

    public int? CantiereId { get; set; }
    public Cantiere? Cantiere { get; set; }

    public ICollection<RigaFattura> Righe { get; set; } = new List<RigaFattura>();
}
