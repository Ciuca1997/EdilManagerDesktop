namespace EdilManager.Core.DTOs;

public class AziendaDto
{
    public string RagioneSociale { get; set; } = string.Empty;
    public string? PartitaIva { get; set; }
    public string? CodiceFiscale { get; set; }
    public string? Indirizzo { get; set; }
    public string? Cap { get; set; }
    public string? Comune { get; set; }
    public string? Provincia { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
}
