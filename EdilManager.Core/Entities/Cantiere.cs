namespace EdilManager.Core.Entities;

public class Cantiere
{
    public int Id { get; set; }
    public string CUP { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;

    public ICollection<Fattura> Fatture { get; set; } = new List<Fattura>();
    public ICollection<Presenza> Presenze { get; set; } = new List<Presenza>();
}
