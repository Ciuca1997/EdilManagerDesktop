namespace EdilManager.Core.DTOs;

public class PresenzaDto
{
    public int Id { get; set; }
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
