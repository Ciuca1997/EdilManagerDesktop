namespace EdilManager.Core.DTOs;

public class DipendenteListDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Cognome { get; set; } = string.Empty;
    public string CodiceFiscale { get; set; } = string.Empty;
    public string? Mansione { get; set; }
    public string? Livello { get; set; }
    public DateOnly DataAssunzione { get; set; }
}

public class DipendenteDetailDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Cognome { get; set; } = string.Empty;
    public string CodiceFiscale { get; set; } = string.Empty;
    public DateOnly DataNascita { get; set; }
    public DateOnly DataAssunzione { get; set; }
    public string? Mansione { get; set; }
    public string? Livello { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
}

public class DipendenteUpsertDto
{
    public string Nome { get; set; } = string.Empty;
    public string Cognome { get; set; } = string.Empty;
    public string CodiceFiscale { get; set; } = string.Empty;
    public DateOnly DataNascita { get; set; }
    public DateOnly DataAssunzione { get; set; }
    public string? Mansione { get; set; }
    public string? Livello { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
}
