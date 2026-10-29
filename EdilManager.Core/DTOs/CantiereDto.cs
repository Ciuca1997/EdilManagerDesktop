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
    public IEnumerable<FatturaListDto> Fatture { get; set; } = [];
}
