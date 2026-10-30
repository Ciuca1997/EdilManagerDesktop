namespace EdilManager.Core.DTOs;

public class CantiereListDto
{
    public int Id { get; set; }
    public string CUP { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public int FattureCount { get; set; }
}

public class CantiereDetailDto
{
    public int Id { get; set; }
    public string CUP { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    // Somma dei totali delle fatture, con le note di credito sottratte.
    public decimal TotaleFatture { get; set; }
    public IEnumerable<FornitoreRiepilogoDto> Fornitori { get; set; } = [];
    // Ore registrate sul cantiere, per dipendente (dalle presenze).
    public IEnumerable<ManodoperaDto> Manodopera { get; set; } = [];
    public IEnumerable<FatturaListDto> Fatture { get; set; } = [];
}

// Quanto ha fatturato un fornitore su un cantiere.
public class FornitoreRiepilogoDto
{
    public int FornitoreId { get; set; }
    public string Denominazione { get; set; } = string.Empty;
    public int FattureCount { get; set; }
    public decimal Totale { get; set; }
}

// Ore lavorate da un dipendente su un cantiere.
public class ManodoperaDto
{
    public int DipendenteId { get; set; }
    public string Dipendente { get; set; } = string.Empty;
    public int Giorni { get; set; }
    public decimal OreOrdinarie { get; set; }
    public decimal OreStraordinarie { get; set; }
}
