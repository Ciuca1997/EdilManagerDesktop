namespace EdilManager.Core.DTOs;

public class FornitoreListDto
{
    public int Id { get; set; }
    public string PartitaIva { get; set; } = string.Empty;
    public string Denominazione { get; set; } = string.Empty;
    public string? Comune { get; set; }
    public string? Provincia { get; set; }
    public int FattureCount { get; set; }
}

public class FornitoreDetailDto
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
    // Somma dei totali delle fatture, con le note di credito sottratte.
    public decimal TotaleFatture { get; set; }
    public IEnumerable<FatturaListDto> Fatture { get; set; } = [];
}
