using EdilManager.Core.DTOs;

namespace EdilManager.Desktop.ViewModels;

// Voce di un elenco a tendina di cantieri; Id nullo = "Nessun cantiere".
public sealed record CantiereOption(int? Id, string Nome)
{
    public static readonly CantiereOption Nessuno = new(null, "Nessun cantiere");

    public static CantiereOption Da(CantiereListDto c) =>
        new(c.Id, c.Etichetta);

    // Elenco completo per un menu: "Nessun cantiere" in testa, poi i cantieri per nome.
    public static List<CantiereOption> Elenco(IEnumerable<CantiereListDto> cantieri) =>
        [Nessuno, .. cantieri.OrderBy(c => c.Nome, StringComparer.CurrentCultureIgnoreCase).Select(Da)];
}
