using EdilManager.Core.Entities;

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
    public StatoPagamento Stato { get; set; }
    public DateOnly? DataPagamento { get; set; }
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
    public StatoPagamento Stato { get; set; }
    public DateOnly? DataPagamento { get; set; }
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
// Copie: percorsi dei file copiati nell'archivio per questo file; AvvisoArchivio: copia non riuscita (l'import resta valido).
public record ImportCartellaProgress(
    int Elaborati, int Totale, string File, EsitoImport? Esito = null, string? Motivo = null, Exception? Eccezione = null,
    IReadOnlyList<string>? Copie = null, string? AvvisoArchivio = null);

// Fattura salvata (o già presente) con fornitore e cantiere come stanno nel database: servono per archiviare il file.
// CantiereCreato = true se il CUP della fattura ha fatto nascere un cantiere nuovo (con il CUP come nome provvisorio).
public record EsitoSalvataggio(bool Nuova, string Fornitore, string? Cantiere, string Numero, DateOnly Data,
    int? CantiereId = null, bool CantiereCreato = false, decimal Totale = 0, string? Descrizione = null);

// Cantiere creato da un import, in attesa che l'utente gli dia un nome; la fattura è quella in cui è stato trovato il CUP.
public record NuovoCantiere(int Id, string Cup, string Fornitore, string Numero, DateOnly Data, decimal Totale, string? Descrizione);

public record FileScartato(string File, string Motivo);

public class ImportCartellaResult
{
    public int Totale { get; set; }
    // File effettivamente elaborati: meno di Totale se l'operazione è stata annullata.
    public int Elaborati { get; set; }
    public bool Annullata { get; set; }
    // File copiati nella cartella archivio (se attiva).
    public int Copiate { get; set; }
    // Copie in archivio non riuscite: l'import di quelle fatture è comunque valido.
    public List<FileScartato> AvvisiArchivio { get; } = [];
    // Cantieri nati da CUP nuovi in questo import (il nome è ancora il CUP).
    public List<NuovoCantiere> NuoviCantieri { get; } = [];
    public int Importate { get; set; }
    public int Duplicate { get; set; }
    // File che non sono fatture valide (altri XML/PDF, importi incoerenti, scansioni...).
    public List<FileScartato> Scartati { get; } = [];
    // Problemi imprevisti (file illeggibile, errore del database).
    public List<FileScartato> Errori { get; } = [];
}
