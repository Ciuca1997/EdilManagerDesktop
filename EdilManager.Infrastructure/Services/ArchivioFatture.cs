using EdilManager.Core.DTOs;

namespace EdilManager.Infrastructure.Services;

public record RisultatoArchivio(IReadOnlyList<string> Copiate, string? Avviso);

// Copia i file delle fatture importate in una cartella scelta dall'utente:
//   <radice>\fornitori\<fornitore>\<data> <fornitore> - <numero>.<ext>
//   <radice>\cantieri\<cantiere>\...   (solo se la fattura ha un cantiere)
// Il nome del file dipende solo dalla fattura, quindi reimportare lo stesso file non crea doppioni e
// copia solo ciò che manca. Se il cantiere viene rinominato, ne viene rinominata anche la cartella.
public class ArchivioFatture(ImpostazioniService impostazioni)
{
    public const string CartellaFornitori = "fornitori";
    public const string CartellaCantieri = "cantieri";
    private const int LunghezzaMassimaNome = 80;

    private static readonly char[] CaratteriNonValidi = Path.GetInvalidFileNameChars();
    private static readonly string[] NomiRiservati =
        ["CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
         "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"];

    public string? Radice =>
        impostazioni.Corrente is { ArchivioAttivo: true, CartellaArchivio: { Length: > 0 } r } ? r : null;

    public RisultatoArchivio Copia(string origine, EsitoSalvataggio fattura)
    {
        if (Radice is not { } radice) return new RisultatoArchivio([], null);

        try
        {
            // Un file già dentro l'archivio non va copiato su se stesso né duplicato nelle altre cartelle.
            if (SottoCartella(Path.GetFullPath(origine), Path.GetFullPath(radice)))
                return new RisultatoArchivio([], null);

            var nomeFile = NomeFile(fattura, Path.GetExtension(origine));
            var destinazioni = new List<string>
            {
                Path.Combine(radice, CartellaFornitori, NomeCartella(fattura.Fornitore), nomeFile),
            };
            if (!string.IsNullOrWhiteSpace(fattura.Cantiere))
                destinazioni.Add(Path.Combine(radice, CartellaCantieri, NomeCartella(fattura.Cantiere), nomeFile));

            var copiate = new List<string>();
            string? avviso = null;
            foreach (var destinazione in destinazioni)
            {
                try
                {
                    if (File.Exists(destinazione)) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(destinazione)!);
                    File.Copy(origine, destinazione);
                    copiate.Add(destinazione);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
                {
                    avviso = $"Copia in archivio non riuscita ({destinazione}): {ex.Message}";
                }
            }
            return new RisultatoArchivio(copiate, avviso);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return new RisultatoArchivio([], $"Copia in archivio non riuscita: {ex.Message}");
        }
    }

    // Restituisce un avviso se la cartella non si è potuta rinominare (il nome nel database è comunque cambiato).
    public string? RinominaCartellaCantiere(string vecchioNome, string nuovoNome)
    {
        if (Radice is not { } radice) return null;
        var vecchia = Path.Combine(radice, CartellaCantieri, NomeCartella(vecchioNome));
        var nuova = Path.Combine(radice, CartellaCantieri, NomeCartella(nuovoNome));
        if (string.Equals(vecchia, nuova, StringComparison.OrdinalIgnoreCase) && vecchia == nuova) return null;

        try
        {
            if (!Directory.Exists(vecchia)) return null;

            // Solo cambio di maiuscole: su Windows le due cartelle sono la stessa, serve un passaggio intermedio.
            if (string.Equals(vecchia, nuova, StringComparison.OrdinalIgnoreCase))
            {
                var temporanea = vecchia + ".rinomina";
                Directory.Move(vecchia, temporanea);
                Directory.Move(temporanea, nuova);
                return null;
            }

            if (!Directory.Exists(nuova))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(nuova)!);
                Directory.Move(vecchia, nuova);
                return null;
            }

            // La cartella con il nuovo nome esiste già (altro cantiere con lo stesso nome): si uniscono i file.
            foreach (var file in Directory.EnumerateFiles(vecchia))
            {
                var destinazione = Path.Combine(nuova, Path.GetFileName(file));
                if (!File.Exists(destinazione)) File.Move(file, destinazione);
            }
            if (!Directory.EnumerateFileSystemEntries(vecchia).Any()) Directory.Delete(vecchia);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return $"Cantiere rinominato, ma la cartella in archivio non si è potuta rinominare ({vecchia}): {ex.Message}";
        }
    }

    // "2024-08-09 Edilcentro Marziali S.r.l - 2089.pdf"
    public static string NomeFile(EsitoSalvataggio fattura, string estensione) =>
        $"{fattura.Data:yyyy-MM-dd} {Pulisci(fattura.Fornitore)} - {Pulisci(fattura.Numero)}{estensione.ToLowerInvariant()}";

    public static string NomeCartella(string nome) => Pulisci(nome);

    // Rende il testo utilizzabile come nome di file/cartella su Windows: niente caratteri vietati, niente punto o
    // spazio finali (Windows li toglie e il percorso non corrisponderebbe più), niente nomi riservati, lunghezza limitata.
    internal static string Pulisci(string testo)
    {
        var pulito = new string(testo.Select(c => CaratteriNonValidi.Contains(c) ? '-' : c).ToArray());
        pulito = string.Join(' ', pulito.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (pulito.Length > LunghezzaMassimaNome) pulito = pulito[..LunghezzaMassimaNome];
        pulito = pulito.TrimEnd('.', ' ').TrimStart(' ');
        if (pulito.Length == 0) pulito = "senza nome";
        if (NomiRiservati.Contains(pulito, StringComparer.OrdinalIgnoreCase)) pulito = "_" + pulito;
        return pulito;
    }

    private static bool SottoCartella(string percorso, string radice)
    {
        var base_ = radice.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return percorso.StartsWith(base_, StringComparison.OrdinalIgnoreCase);
    }
}
