namespace EdilManager.Core.DTOs;

public class PresenzaDto
{
    public int Id { get; set; }
    public int DipendenteId { get; set; }
    public DateOnly Data { get; set; }
    public decimal OreOrdinarie { get; set; }
    public decimal OreStatordinarie { get; set; }
    public string? Cantiere { get; set; }
    public int? CantiereId { get; set; }
}

public class PresenzaUpsertDto
{
    public int DipendenteId { get; set; }
    public int? CantiereId { get; set; }
    public DateOnly Data { get; set; }
    public decimal OreOrdinarie { get; set; }
    public decimal OreStatordinarie { get; set; }
}

// Una riga di un giorno: le ore svolte su un cantiere (o senza cantiere). Un giorno può averne più d'una.
public class PresenzaGiornoRigaDto
{
    public int? CantiereId { get; set; }
    public decimal OreOrdinarie { get; set; }
    public decimal OreStraordinarie { get; set; }
}

public enum TipoOre { Ordinarie, Straordinarie }

public record CellaPresenza(int DipendenteId, DateOnly Data);

public record EsitoAssegnazioneCantiere(int Aggiornate, int Saltate);

public enum FormatoExport { Pdf, Csv }
