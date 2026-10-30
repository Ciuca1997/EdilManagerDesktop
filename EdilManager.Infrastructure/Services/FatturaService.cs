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
            .Select(f => new FatturaListDto
            {
                Id = f.Id,
                NumeroDocumento = f.NumeroDocumento,
                DataDocumento = f.DataDocumento,
                TipoDocumento = f.TipoDocumento,
                TotaleDocumento = f.TotaleDocumento,
                ModalitaPagamento = f.ModalitaPagamento,
                DataScadenzaPagamento = f.DataScadenzaPagamento,
                Fornitore = f.Fornitore.Denominazione,
                Cantiere = f.Cantiere != null ? f.Cantiere.Nome : null,
            })
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
    public async Task<bool> ImportaAsync(Stream contenuto, string nomeFile, CancellationToken ct = default)
    {
        var estensione = Path.GetExtension(nomeFile).ToLowerInvariant();
        var isPdf = estensione == ".pdf";
        var (fornitore, fattura, cup) = estensione switch
        {
            ".xml" => ParseXml(contenuto),
            ".pdf" => ParsePdf(contenuto),
            _ => throw new InvalidOperationException($"Formato «{estensione}» non supportato (solo XML e PDF)."),
        };

        await using var db = await factory.CreateDbContextAsync(ct);
        var prima = await db.Fatture.CountAsync(ct);
        var import = new PecImportService(db, NullLogger<PecImportService>.Instance);
        await import.SalvaFatturaAsync(fornitore, fattura, cup, ct, aggiornaAnagrafica: !isPdf);
        return await db.Fatture.CountAsync(ct) > prima;
    }

    private static (Fornitore, Fattura, string?) ParsePdf(Stream pdf) => FatturaPdfParser.Parse(pdf);

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
