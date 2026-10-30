using System.Text.RegularExpressions;

namespace EdilManager.Core;

// Controlli formali sui dati fiscali (non verificano che il soggetto esista, solo che il formato sia plausibile).
public static partial class DatiFiscali
{
    // 11 cifre con la cifra di controllo corretta.
    public static bool PartitaIvaValida(string? piva)
    {
        if (piva is null || piva.Length != 11 || !piva.All(char.IsDigit)) return false;
        var somma = 0;
        for (var i = 0; i < 10; i++)
        {
            var cifra = piva[i] - '0';
            if (i % 2 == 1) { cifra *= 2; if (cifra > 9) cifra -= 9; }
            somma += cifra;
        }
        return (10 - somma % 10) % 10 == piva[10] - '0';
    }

    // Persona fisica (16 caratteri) oppure società (11 cifre, come la partita IVA).
    public static bool CodiceFiscaleValido(string? codiceFiscale) =>
        codiceFiscale is not null && (PersonaFisica().IsMatch(codiceFiscale) || codiceFiscale.Length == 11 && codiceFiscale.All(char.IsDigit));

    [GeneratedRegex(@"^[A-Z]{6}\d{2}[A-Z]\d{2}[A-Z]\d{3}[A-Z]$")]
    private static partial Regex PersonaFisica();
}
