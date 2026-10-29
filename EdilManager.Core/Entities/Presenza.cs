namespace EdilManager.Core.Entities;

public class Presenza
{
    public int Id { get; set; }
    public int DipendenteId { get; set; }
    public Dipendente Dipendente { get; set; } = null!;
    public int? CantiereId { get; set; }
    public Cantiere? Cantiere { get; set; }
    public DateOnly Data { get; set; }
    public decimal OreOrdinarie { get; set; }
    public decimal OreStatordinarie { get; set; }
}
