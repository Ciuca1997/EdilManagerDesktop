using System.Text.RegularExpressions;

namespace EdilManager.Core;

public static class Cup
{
    // 15 caratteri: lettera, 2 cifre, lettera, 11 cifre (come nel parser delle fatture).
    private static readonly Regex Formato = new(@"^[A-Z]\d{2}[A-Z]\d{11}$", RegexOptions.CultureInvariant);

    public static string Normalizza(string? cup) => (cup ?? string.Empty).Trim().ToUpperInvariant();

    public static bool Valido(string? cup) => Formato.IsMatch(Normalizza(cup));
}
