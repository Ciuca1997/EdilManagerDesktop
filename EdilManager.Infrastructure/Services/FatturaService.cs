using System.Xml;
using EdilManager.Core.DTOs;
using EdilManager.Core.Entities;
using EdilManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EdilManager.Infrastructure.Services;

public class FatturaService(IDbContextFactory<ApplicationDbContext> factory)
{
    public async Task<List<FatturaListDto>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Fatture
            .OrderByDescending(f => f.DataDocumento).ThenByDescending(f => f.Id)
            .Select(FatturaProjections.ToListDto)
            .ToListAsync(ct);
    }

    // Il fornitore è senza elenco fatture (Fatture resta vuoto).
    public async Task<FatturaDetailDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Fatture
            .Where(f => f.Id == id)
            .Select(f => new FatturaDetailDto
            {
                Id = f.Id,
                NumeroDocumento = f.NumeroDocumento,
                DataDocumento = f.DataDocumento,
                TipoDocumento = f.TipoDocumento,
                TotaleImponibile = f.TotaleImponibile,
                TotaleImposta = f.TotaleImposta,
                TotaleDocumento = f.TotaleDocumento,
                ModalitaPagamento = f.ModalitaPagamento,
                DataScadenzaPagamento = f.DataScadenzaPagamento,
                ImportoPagamento = f.ImportoPagamento,
                Fornitore = new FornitoreDetailDto
                {
                    Id = f.Fornitore.Id,
                    PartitaIva = f.Fornitore.PartitaIva,
                    CodiceFiscale = f.Fornitore.CodiceFiscale,
                    Denominazione = f.Fornitore.Denominazione,
                    Indirizzo = f.Fornitore.Indirizzo,
                    Comune = f.Fornitore.Comune,
                    Provincia = f.Fornitore.Provincia,
                    Cap = f.Fornitore.Cap,
                    Email = f.Fornitore.Email,
                },
                Cantiere = f.Cantiere == null
                    ? null
                    : new CantiereListDto { Id = f.Cantiere.Id, CUP = f.Cantiere.CUP, Nome = f.Cantiere.Nome },
                Righe = f.Righe.OrderBy(r => r.Id).Select(r => new RigaFatturaDto
                {
                    Id = r.Id,
                    CodiceArticolo = r.CodiceArticolo,
                    Descrizione = r.Descrizione,
                    Quantita = r.Quantita,
                    PrezzoUnitario = r.PrezzoUnitario,
                    UnitaMisura = r.UnitaMisura,
                    Sconto = r.Sconto,
                    AliquotaIVA = r.AliquotaIVA,
                    PrezzoTotale = r.PrezzoTotale,
                }).ToList(),
            })
            .FirstOrDefaultAsync(ct);
    }

    // Sceglie il parser in base all'estensione del file.
    // Restituisce false se la fattura (stesso fornitore, numero e data) era già presente.
    public async Task<bool> ImportaAsync(Stream contenuto, string nomeFile, CancellationToken ct = default,
        bool pdfRigoroso = false)
    {
        var estensione = Path.GetExtension(nomeFile).ToLowerInvariant();
        var isPdf = estensione == ".pdf";
        var (fornitore, fattura, cup) = estensione switch
        {
            ".xml" => ParseXml(contenuto),
            ".pdf" => ParsePdf(contenuto, pdfRigoroso),
            _ => throw new InvalidOperationException($"Formato «{estensione}» non supportato (solo XML e PDF)."),
        };

        await using var db = await factory.CreateDbContextAsync(ct);
        var prima = await db.Fatture.CountAsync(ct);
        var import = new PecImportService(db, NullLogger<PecImportService>.Instance);
        await import.SalvaFatturaAsync(fornitore, fattura, cup, ct, aggiornaAnagrafica: !isPdf);
        return await db.Fatture.CountAsync(ct) > prima;
    }

    // Cerca fatture XML e PDF nella cartella e in tutte le sottocartelle e importa quelle valide; il resto viene scartato
    // con il motivo. I PDF non standard si accettano solo se gli importi sono coerenti (contratti e preventivi non passano).
    public Task<ImportCartellaResult> ImportaCartellaAsync(string cartella,
        IProgress<ImportCartellaProgress>? progress = null, CancellationToken ct = default)
    {
        if (!Directory.Exists(cartella))
            throw new DirectoryNotFoundException($"La cartella «{cartella}» non esiste.");

        var opzioni = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
        var file = Directory.EnumerateFiles(cartella, "*", opzioni)
            .Where(f => Path.GetExtension(f).ToLowerInvariant() is ".xml" or ".pdf");
        return ImportaElencoAsync(file, cartella, pdfRigoroso: true, progress, ct);
    }

    // Importa i file scelti dall'utente: qui un PDF non standard viene letto con le regole generiche anche se meno certo.
    public Task<ImportCartellaResult> ImportaFileAsync(IEnumerable<string> file,
        IProgress<ImportCartellaProgress>? progress = null, CancellationToken ct = default) =>
        ImportaElencoAsync(file, null, pdfRigoroso: false, progress, ct);

    // Gli XML vanno prima dei PDF: se esistono entrambi per la stessa fattura vince l'XML, più completo
    // (il PDF verrebbe poi riconosciuto come duplicato).
    // L'annullamento si controlla tra un file e l'altro: ogni file è importato per intero oppure per niente,
    // e il risultato parziale viene restituito (Annullata = true) invece di lanciare un'eccezione.
    private async Task<ImportCartellaResult> ImportaElencoAsync(IEnumerable<string> elenco, string? radice,
        bool pdfRigoroso, IProgress<ImportCartellaProgress>? progress, CancellationToken ct)
    {
        var file = elenco
            .OrderBy(f => Path.GetExtension(f).Equals(".xml", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var risultato = new ImportCartellaResult { Totale = file.Count };
        progress?.Report(new ImportCartellaProgress(0, file.Count, string.Empty));

        foreach (var percorso in file)
        {
            if (ct.IsCancellationRequested)
            {
                risultato.Annullata = true;
                break;
            }

            var nome = radice is null ? Path.GetFileName(percorso) : Path.GetRelativePath(radice, percorso);
            progress?.Report(new ImportCartellaProgress(risultato.Elaborati, file.Count, nome));

            EsitoImport esito;
            string? motivo = null;
            Exception? eccezione = null;
            try
            {
                if (new FileInfo(percorso).Length > DimensioneMassima)
                {
                    esito = EsitoImport.Scartata;
                    motivo = "File troppo grande per essere una fattura.";
                }
                else
                {
                    // La lettura del PDF è pesante: fuori dal thread dell'interfaccia.
                    var nuova = await Task.Run(async () =>
                    {
                        await using var stream = File.OpenRead(percorso);
                        return await ImportaAsync(stream, percorso, CancellationToken.None, pdfRigoroso);
                    }, CancellationToken.None);
                    esito = nuova ? EsitoImport.Importata : EsitoImport.Duplicata;
                }
            }
            catch (InvalidOperationException ex)
            {
                (esito, motivo) = (EsitoImport.Scartata, ex.Message);
            }
            catch (Exception ex)
            {
                (esito, motivo, eccezione) = (EsitoImport.Errore, ex.Message, ex);
            }

            switch (esito)
            {
                case EsitoImport.Importata: risultato.Importate++; break;
                case EsitoImport.Duplicata: risultato.Duplicate++; break;
                case EsitoImport.Scartata: risultato.Scartati.Add(new FileScartato(nome, motivo!)); break;
                default: risultato.Errori.Add(new FileScartato(nome, motivo!)); break;
            }
            risultato.Elaborati++;
            progress?.Report(new ImportCartellaProgress(risultato.Elaborati, file.Count, nome, esito, motivo, eccezione));
        }

        return risultato;
    }

    private const long DimensioneMassima = 30 * 1024 * 1024;

    private static (Fornitore, Fattura, string?) ParsePdf(Stream pdf, bool rigoroso) => FatturaPdfParser.Parse(pdf, rigoroso);

    private static (Fornitore, Fattura, string?) ParseXml(Stream xml)
    {
        const string nonValida = "Il file non è una fattura elettronica FatturaPA valida.";
        try
        {
            var parsed = FatturaPaParser.Parse(xml);

            // Il parser tollera i campi mancanti: senza questi dati non è una fattura utilizzabile.
            if (string.IsNullOrWhiteSpace(parsed.fornitore.PartitaIva) ||
                string.IsNullOrWhiteSpace(parsed.fattura.NumeroDocumento) ||
                parsed.fattura.DataDocumento == default)
                throw new InvalidOperationException(nonValida);

            return parsed;
        }
        catch (Exception ex) when (ex is NullReferenceException or XmlException)
        {
            throw new InvalidOperationException(nonValida, ex);
        }
    }
}
