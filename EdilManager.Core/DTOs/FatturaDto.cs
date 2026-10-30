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
    public int FornitoreId { get; set; }
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

public enum EsitoImport { Importata, Duplicata, Scartata, Errore }

// Esito null = il file sta per essere elaborato; altrimenti è il risultato di quel file.
public record ImportCartellaProgress(
    int Elaborati, int Totale, string File, EsitoImport? Esito = null, string? Motivo = null, Exception? Eccezione = null);

public record FileScartato(string File, string Motivo);

public class ImportCartellaResult
{
    public int Totale { get; set; }
    // File effettivamente elaborati: meno di Totale se l'operazione è stata annullata.
    public int Elaborati { get; set; }
    public bool Annullata { get; set; }
    public int Importate { get; set; }
    public int Duplicate { get; set; }
    // File che non sono fatture valide (altri XML/PDF, importi incoerenti, scansioni...).
    public List<FileScartato> Scartati { get; } = [];
    // Problemi imprevisti (file illeggibile, errore del database).
    public List<FileScartato> Errori { get; } = [];
}
