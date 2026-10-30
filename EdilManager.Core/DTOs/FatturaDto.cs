namespace EdilManager.Core.DTOs;

public class FatturaListDto
{
    public int Id { get; set; }
    public string NumeroDocumento { get; set; } = string.Empty;
    public DateOnly DataDocumento { get; set; }
    public string TipoDocumento { get; set; } = string.Empty;
    public decimal TotaleDocumento { get; set; }
    public string? ModalitaPagamento { get; set; }
    public DateOnly? DataScadenzaPagamento { get; set; }
    public string Fornitore { get; set; } = string.Empty;
    public string? Cantiere { get; set; }
}

public class FatturaDetailDto
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
    public FornitoreDetailDto Fornitore { get; set; } = null!;
    public CantiereListDto? Cantiere { get; set; }
    public IEnumerable<RigaFatturaDto> Righe { get; set; } = [];
}

public class RigaFatturaDto
{
    public int Id { get; set; }
    public string? CodiceArticolo { get; set; }
    public string Descrizione { get; set; } = string.Empty;
    public decimal? Quantita { get; set; }
    public decimal? PrezzoUnitario { get; set; }
    public string? UnitaMisura { get; set; }
    public decimal? Sconto { get; set; }
    public decimal AliquotaIVA { get; set; }
    public decimal PrezzoTotale { get; set; }
}
