using System.Globalization;
using System.Text;
using EdilManager.Core;
using EdilManager.Core.DTOs;
using EdilManager.Core.Entities;
using EdilManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EdilManager.Infrastructure.Services;

public enum DivisioneExport
{
    Nessuna,
    PerCantiere,
    PerFornitore,
    // Cartella del cantiere e dentro quella di ogni fornitore.
    PerCantiereEFornitore,
}

public record EsitoEsportazioneFatture(
    string Cartella, int Fatture, int FileCopiati, int FattureSenzaFile, bool ArchivioDisponibile, string? Avviso, bool RiepilogoScritto);

// Esporta le fatture di un periodo (per data della fattura) in una cartella: copia i file originali (XML/PDF) dall'archivio
// fatture, divisi come richiesto, e può aggiungere un riepilogo CSV. I file originali non stanno nel database: se la fattura
// non è mai stata archiviata (archivio spento al momento dell'import) il suo file non c'è, ma compare comunque nel riepilogo.
public class EsportaFattureService(IDbContextFactory<ApplicationDbContext> factory, ImpostazioniService impostazioni)
{
    private const string SenzaCantiere = "Senza cantiere";
    private const string NomeRiepilogo = "Riepilogo fatture.csv";
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    private sealed record Riga(
        int Id, DateOnly Data, string Numero, string Tipo, decimal Imponibile, decimal Imposta, decimal Totale,
        DateOnly? Scadenza, StatoPagamento Stato, DateOnly? DataPagamento, string Fornitore, string PartitaIva, string? Cantiere);

    // Anni in cui ci sono fatture, dal più recente (l'anno corrente c'è sempre).
    public async Task<List<int>> GetAnniAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var date = await db.Fatture.Select(f => f.DataDocumento).ToListAsync(ct);
        var anni = date.Select(d => d.Year).Append(DateTime.Today.Year).Distinct().OrderByDescending(a => a).ToList();
        return anni;
    }

    public async Task<EsitoEsportazioneFatture> EsportaAsync(DateOnly da, DateOnly a, string cartellaDestinazione,
        DivisioneExport divisione, bool riepilogoCsv, CancellationToken ct = default)
    {
        if (a < da) throw new InvalidOperationException("La data finale è precedente a quella iniziale.");

        List<Riga> righe;
        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            righe = await db.Fatture
                .Where(f => f.DataDocumento >= da && f.DataDocumento <= a)
                .OrderBy(f => f.DataDocumento).ThenBy(f => f.Id)
                .Select(f => new Riga(f.Id, f.DataDocumento, f.NumeroDocumento, f.TipoDocumento, f.TotaleImponibile,
                    f.TotaleImposta, f.TotaleDocumento, f.DataScadenzaPagamento, f.Stato, f.DataPagamento,
                    f.Fornitore.Denominazione, f.Fornitore.PartitaIva, f.Cantiere != null ? f.Cantiere.Nome : null))
                .ToListAsync(ct);
        }
        if (righe.Count == 0)
            throw new InvalidOperationException("Non ci sono fatture con la data della fattura in questo periodo.");

        var radiceArchivio = impostazioni.Corrente.CartellaArchivio is { Length: > 0 } r && Directory.Exists(r) ? r : null;
        var giorni = impostazioni.Corrente.GiorniScadenzaPredefiniti;

        // Una sottocartella col periodo, così l'esportazione non si mischia ad altri file della cartella scelta.
        var cartella = Path.Combine(cartellaDestinazione, ArchivioFatture.Pulisci(NomeCartellaPeriodo(da, a)));

        return await Task.Run(() =>
        {
            Directory.CreateDirectory(cartella);
            var elenchi = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            var trovato = new HashSet<int>();
            var copiati = 0;
            string? avviso = null;

            foreach (var f in righe)
            {
                ct.ThrowIfCancellationRequested();
                if (radiceArchivio is null) break;

                var origini = FileInArchivio(radiceArchivio, f, elenchi);
                if (origini.Count == 0) continue;
                trovato.Add(f.Id);

                var destinazione = Path.Combine(cartella, Sottocartelle(f, divisione));
                foreach (var origine in origini)
                {
                    try
                    {
                        Directory.CreateDirectory(destinazione);
                        // Mai sovrascrivere: se il file c'è ed è identico va bene così, se è diverso prende un suffisso.
                        if (ArchivioFatture.PercorsoLibero(origine, Path.Combine(destinazione, Path.GetFileName(origine))) is { } percorso)
                            File.Copy(origine, percorso);
                        copiati++;
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
                    {
                        avviso ??= $"Copia non riuscita ({Path.GetFileName(origine)}): {ex.Message}";
                    }
                }
            }

            var riepilogoScritto = false;
            if (riepilogoCsv)
            {
                File.WriteAllBytes(Path.Combine(cartella, NomeRiepilogo), Riepilogo(righe, trovato, giorni));
                riepilogoScritto = true;
            }

            return new EsitoEsportazioneFatture(cartella, righe.Count, copiati, righe.Count - trovato.Count,
                radiceArchivio is not null, avviso, riepilogoScritto);
        }, CancellationToken.None);
    }

    // "Fatture 2026" per un anno intero, altrimenti "Fatture 2026-01-01 - 2026-06-30".
    internal static string NomeCartellaPeriodo(DateOnly da, DateOnly a) =>
        da is { Month: 1, Day: 1 } && a is { Month: 12, Day: 31 } && da.Year == a.Year
            ? $"Fatture {da.Year}"
            : $"Fatture {da:yyyy-MM-dd} - {a:yyyy-MM-dd}";

    private static string Sottocartelle(Riga f, DivisioneExport divisione)
    {
        var cantiere = ArchivioFatture.NomeCartella(f.Cantiere ?? SenzaCantiere);
        var fornitore = ArchivioFatture.NomeCartella(f.Fornitore);
        return divisione switch
        {
            DivisioneExport.PerCantiere => cantiere,
            DivisioneExport.PerFornitore => fornitore,
            DivisioneExport.PerCantiereEFornitore => Path.Combine(cantiere, fornitore),
            _ => string.Empty,
        };
    }

    // File della fattura nell'archivio: nella cartella del fornitore, col nome "<data> <fornitore> - <numero>.<estensione>".
    private static List<string> FileInArchivio(string radice, Riga f, Dictionary<string, string[]> elenchi)
    {
        var cartellaFornitore = Path.Combine(radice, ArchivioFatture.CartellaFornitori, ArchivioFatture.NomeCartella(f.Fornitore));
        if (!elenchi.TryGetValue(cartellaFornitore, out var file))
        {
            file = Directory.Exists(cartellaFornitore) ? Directory.GetFiles(cartellaFornitore) : [];
            elenchi[cartellaFornitore] = file;
        }

        var nomeBase = Path.GetFileNameWithoutExtension(
            ArchivioFatture.NomeFile(new EsitoSalvataggio(false, f.Fornitore, null, f.Numero, f.Data), ".xml"));
        return file.Where(p => string.Equals(Path.GetFileNameWithoutExtension(p), nomeBase, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    // CSV per Excel: separatore ";", virgola decimale e BOM. Le note di credito hanno il totale negativo.
    private static byte[] Riepilogo(List<Riga> righe, HashSet<int> conFile, int giorniScadenza)
    {
        var oggi = DateOnly.FromDateTime(DateTime.Today);
        var testo = new StringBuilder();
        testo.AppendLine(string.Join(';', "Data", "Numero", "Fornitore", "P.IVA", "Cantiere", "Tipo", "Imponibile", "IVA",
            "Totale (note di credito in negativo)", "Scadenza", "Stato", "Data pagamento", "File esportato"));
        foreach (var f in righe)
        {
            var segno = Documenti.ConSegno(f.Tipo, 1m);
            var stato = Pagamenti.Calcola(f.Tipo, f.Stato, f.Scadenza, f.Data, giorniScadenza, oggi);
            testo.AppendLine(string.Join(';',
                f.Data.ToString("dd/MM/yyyy", It), Csv(f.Numero), Csv(f.Fornitore), Csv(f.PartitaIva), Csv(f.Cantiere ?? string.Empty),
                f.Tipo, Importo(f.Imponibile * segno), Importo(f.Imposta * segno), Importo(f.Totale * segno),
                f.Scadenza?.ToString("dd/MM/yyyy", It) ?? string.Empty, Pagamenti.Testo(stato) is var t && t != "-" ? t : string.Empty,
                f.DataPagamento is { } dp && f.Stato == StatoPagamento.Pagata ? dp.ToString("dd/MM/yyyy", It) : string.Empty,
                conFile.Contains(f.Id) ? "sì" : "no"));
        }
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(testo.ToString())];
    }

    private static string Importo(decimal valore) => valore.ToString("0.00", It);

    private static string Csv(string testo) =>
        testo.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? $"\"{testo.Replace("\"", "\"\"")}\"" : testo;
}
