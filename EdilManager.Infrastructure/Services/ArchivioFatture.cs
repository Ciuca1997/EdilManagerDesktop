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
                    if (PercorsoLibero(origine, destinazione) is not { } percorso) continue; // già presente, identico
                    Directory.CreateDirectory(Path.GetDirectoryName(percorso)!);
                    File.Copy(origine, percorso);
                    copiate.Add(percorso);
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

    // Una fattura assegnata a mano a un cantiere: i file già in archivio (cartella del fornitore) vengono copiati anche nella
    // cartella del cantiere, che nasce così alla prima fattura. Non si cancella nulla dalle cartelle di altri cantieri.
    // Restituisce un avviso se qualche copia non è riuscita.
    public string? CopiaInCantiere(EsitoSalvataggio fattura, string cantiere)
    {
        if (Radice is not { } radice) return null;
        try
        {
            var cartellaFornitore = Path.Combine(radice, CartellaFornitori, NomeCartella(fattura.Fornitore));
            if (!Directory.Exists(cartellaFornitore)) return null;

            var baseNome = Path.GetFileNameWithoutExtension(NomeFile(fattura, ".xml"));
            string? avviso = null;
            foreach (var origine in Directory.EnumerateFiles(cartellaFornitore))
            {
                if (!string.Equals(Path.GetFileNameWithoutExtension(origine), baseNome, StringComparison.OrdinalIgnoreCase)) continue;
                var destinazione = Path.Combine(radice, CartellaCantieri, NomeCartella(cantiere), Path.GetFileName(origine));
                try
                {
                    if (PercorsoLibero(origine, destinazione) is not { } percorso) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(percorso)!);
                    File.Copy(origine, percorso);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
                {
                    avviso = $"Copia in archivio non riuscita ({destinazione}): {ex.Message}";
                }
            }
            return avviso;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return $"Copia in archivio non riuscita: {ex.Message}";
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

    // Il nome dipende solo da data, fornitore e numero ripuliti: due fatture diverse possono dare lo stesso nome (es. numeri
    // "12/A" e "12-A"). Se il file esiste ed è identico non si copia (restituisce null: reimportare non crea doppioni);
    // se esiste ma è diverso si cerca un nome libero aggiungendo " (2)", " (3)", ... invece di scartare la copia in silenzio.
    internal static string? PercorsoLibero(string origine, string destinazione)
    {
        var cartella = Path.GetDirectoryName(destinazione)!;
        var nome = Path.GetFileNameWithoutExtension(destinazione);
        var estensione = Path.GetExtension(destinazione);
        for (var n = 1; n <= 50; n++)
        {
            var candidato = n == 1 ? destinazione : Path.Combine(cartella, $"{nome} ({n}){estensione}");
            if (!File.Exists(candidato)) return candidato;
            if (StessoContenuto(origine, candidato)) return null;
        }
        throw new IOException("Troppi file con lo stesso nome nella cartella dell'archivio.");
    }

    private static bool StessoContenuto(string a, string b)
    {
        if (new FileInfo(a).Length != new FileInfo(b).Length) return false;
        using var fa = File.OpenRead(a);
        using var fb = File.OpenRead(b);
        var ba = new byte[81920];
        var bb = new byte[81920];
        int letti;
        while ((letti = fa.Read(ba, 0, ba.Length)) > 0)
        {
            var pari = 0;
            while (pari < letti)
            {
                var n = fb.Read(bb, pari, letti - pari);
                if (n == 0) return false;
                pari += n;
            }
            if (!ba.AsSpan(0, letti).SequenceEqual(bb.AsSpan(0, letti))) return false;
        }
        return true;
    }

    // File della fattura nella cartella del fornitore (PDF prima dell'XML, perché è il più leggibile). Vuoto se l'archivio
    // è spento o la fattura non è stata archiviata.
    public IReadOnlyList<string> TrovaFile(string fornitore, string numero, DateOnly data)
    {
        if (Radice is not { } radice) return [];
        try
        {
            var cartella = Path.Combine(radice, CartellaFornitori, NomeCartella(fornitore));
            if (!Directory.Exists(cartella)) return [];
            var nomeBase = Path.GetFileNameWithoutExtension(
                NomeFile(new EsitoSalvataggio(false, fornitore, null, numero, data), ".xml"));
            return Directory.EnumerateFiles(cartella)
                .Where(p => string.Equals(Path.GetFileNameWithoutExtension(p), nomeBase, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => string.Equals(Path.GetExtension(p), ".pdf", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return [];
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
